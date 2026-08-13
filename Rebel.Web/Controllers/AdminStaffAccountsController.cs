using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;
using Rebel.Web.Authorization;
using Rebel.Web.Models;

namespace Rebel.Web.Controllers
{
    [Authorize(Policy = AdminPolicies.ManagerOnly)]
    public class AdminStaffAccountsController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly AppDbContext _context;

        public AdminStaffAccountsController(
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            AppDbContext context)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            CancellationToken cancellationToken)
        {
            var users = await _userManager.Users
                .OrderBy(user => user.Email)
                .ToListAsync(cancellationToken);
            var accounts = new List<AdminStaffAccountRowViewModel>();
            var currentUserId = _userManager.GetUserId(User);
            var profiles = await _context.StaffMembers
                .AsNoTracking()
                .OrderByDescending(staff => staff.IsActive)
                .ThenBy(staff => staff.Role)
                .ThenBy(staff => staff.FullName)
                .ToListAsync(cancellationToken);
            var profilesById = profiles.ToDictionary(profile => profile.Id);
            var linkedAccounts = new Dictionary<Guid, string>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var claims = await _userManager.GetClaimsAsync(user);
                var isOwner = roles.Contains(AdminRoles.LegacyAdmin);

                if (!isOwner &&
                    !roles.Contains(AdminRoles.Manager) &&
                    !roles.Contains(AdminRoles.Staff))
                {
                    continue;
                }

                var role = isOwner || roles.Contains(AdminRoles.Manager)
                    ? AdminRoles.Manager
                    : AdminRoles.Staff;
                var staffMemberId = GetStaffMemberId(claims);

                if (staffMemberId.HasValue)
                {
                    linkedAccounts.TryAdd(staffMemberId.Value, user.Id);
                }

                accounts.Add(new AdminStaffAccountRowViewModel
                {
                    Id = user.Id,
                    DisplayName = claims
                        .FirstOrDefault(claim =>
                            claim.Type == AdminRoles.DisplayNameClaim)
                        ?.Value ?? user.Email ?? user.UserName ?? "Staff member",
                    Email = user.Email ?? user.UserName ?? string.Empty,
                    Role = role,
                    IsDisabled = user.LockoutEnd > DateTimeOffset.UtcNow,
                    IsCurrentUser = user.Id == currentUserId,
                    IsOwner = isOwner,
                    StaffMemberId = staffMemberId,
                    StaffMemberName = staffMemberId.HasValue &&
                                      profilesById.TryGetValue(staffMemberId.Value, out var profile)
                        ? profile.FullName
                        : null
                });
            }

            return View(new AdminStaffAccountsViewModel
            {
                Accounts = accounts,
                StaffProfiles = profiles
                    .Select(profile => new StaffAccountProfileOptionViewModel
                    {
                        Id = profile.Id,
                        Name = profile.FullName,
                        Section = profile.Role.ToString(),
                        IsActive = profile.IsActive,
                        LinkedAccountId = linkedAccounts.GetValueOrDefault(profile.Id)
                    })
                    .ToList()
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            AdminStaffAccountInputModel input)
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] =
                    "Account could not be created. Check the name, email, role and password.";
                return RedirectToAction(nameof(Index));
            }

            var email = input.Email.Trim().ToLowerInvariant();

            if (!input.StaffMemberId.HasValue ||
                !await _context.StaffMembers.AnyAsync(
                    staff => staff.Id == input.StaffMemberId.Value && staff.IsActive))
            {
                TempData["ErrorMessage"] = "Choose an active staff profile for this account.";
                return RedirectToAction(nameof(Index));
            }

            if (await IsProfileLinkedAsync(input.StaffMemberId.Value, null))
            {
                TempData["ErrorMessage"] = "That staff profile already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            if (await _userManager.FindByEmailAsync(email) != null)
            {
                TempData["ErrorMessage"] = "An account with that email already exists.";
                return RedirectToAction(nameof(Index));
            }

            var user = new IdentityUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                LockoutEnabled = true
            };

            var createResult = await _userManager.CreateAsync(
                user,
                input.TemporaryPassword);

            if (!createResult.Succeeded)
            {
                TempData["ErrorMessage"] = JoinErrors(createResult);
                return RedirectToAction(nameof(Index));
            }

            var roleResult = await _userManager.AddToRoleAsync(user, input.Role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                TempData["ErrorMessage"] = JoinErrors(roleResult);
                return RedirectToAction(nameof(Index));
            }

            var claimResult = await _userManager.AddClaimsAsync(
                user,
                new[]
                {
                    new Claim(
                        AdminRoles.DisplayNameClaim,
                        input.DisplayName.Trim()),
                    new Claim(
                        AdminRoles.StaffMemberIdClaim,
                        input.StaffMemberId.Value.ToString())
                });

            if (!claimResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);
                TempData["ErrorMessage"] = JoinErrors(claimResult);
                return RedirectToAction(nameof(Index));
            }

            TempData["SuccessMessage"] =
                $"{input.DisplayName.Trim()}'s {input.Role.ToLowerInvariant()} account is ready.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LinkProfile(
            string id,
            Guid staffMemberId)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            var profile = await _context.StaffMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(staff => staff.Id == staffMemberId);

            if (profile == null || !profile.IsActive)
            {
                TempData["ErrorMessage"] = "Choose an active staff profile.";
                return RedirectToAction(nameof(Index));
            }

            if (await IsProfileLinkedAsync(staffMemberId, user.Id))
            {
                TempData["ErrorMessage"] = "That staff profile already has a login account.";
                return RedirectToAction(nameof(Index));
            }

            var claims = await _userManager.GetClaimsAsync(user);
            var oldClaims = claims
                .Where(claim => claim.Type == AdminRoles.StaffMemberIdClaim)
                .ToList();

            if (oldClaims.Count > 0)
            {
                var removeResult = await _userManager.RemoveClaimsAsync(user, oldClaims);

                if (!removeResult.Succeeded)
                {
                    TempData["ErrorMessage"] = JoinErrors(removeResult);
                    return RedirectToAction(nameof(Index));
                }
            }

            var result = await _userManager.AddClaimAsync(
                user,
                new Claim(AdminRoles.StaffMemberIdClaim, staffMemberId.ToString()));

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);

                if (user.Id == _userManager.GetUserId(User))
                {
                    await _signInManager.RefreshSignInAsync(user);
                }
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"{user.Email} is linked to {profile.FullName}."
                    : JoinErrors(result);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeRole(
            string id,
            string role)
        {
            if (role != AdminRoles.Staff && role != AdminRoles.Manager)
            {
                return BadRequest();
            }

            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User) ||
                await _userManager.IsInRoleAsync(user, AdminRoles.LegacyAdmin))
            {
                TempData["ErrorMessage"] =
                    "The signed-in owner account cannot change its own access.";
                return RedirectToAction(nameof(Index));
            }

            var currentRoles = await _userManager.GetRolesAsync(user);

            if (!currentRoles.Contains(role))
            {
                var addResult = await _userManager.AddToRoleAsync(user, role);

                if (!addResult.Succeeded)
                {
                    TempData["ErrorMessage"] = JoinErrors(addResult);
                    return RedirectToAction(nameof(Index));
                }
            }

            var rolesToRemove = currentRoles
                .Where(currentRole =>
                    currentRole != role &&
                    (currentRole == AdminRoles.Staff ||
                     currentRole == AdminRoles.Manager))
                .ToList();

            var result = rolesToRemove.Count == 0
                ? IdentityResult.Success
                : await _userManager.RemoveFromRolesAsync(user, rolesToRemove);

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"{user.Email} is now {role}."
                    : JoinErrors(result);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            string id,
            string temporaryPassword)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(temporaryPassword) ||
                temporaryPassword.Length < 8)
            {
                TempData["ErrorMessage"] =
                    "New password must contain at least 8 characters.";
                return RedirectToAction(nameof(Index));
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(
                user,
                token,
                temporaryPassword);

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? $"Password updated for {user.Email}."
                    : JoinErrors(result);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAccess(string id)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            if (user.Id == _userManager.GetUserId(User) ||
                await _userManager.IsInRoleAsync(user, AdminRoles.LegacyAdmin))
            {
                TempData["ErrorMessage"] =
                    "The signed-in owner account cannot be disabled.";
                return RedirectToAction(nameof(Index));
            }

            var isDisabled = user.LockoutEnd > DateTimeOffset.UtcNow;
            var result = await _userManager.SetLockoutEndDateAsync(
                user,
                isDisabled ? null : DateTimeOffset.MaxValue);

            if (result.Succeeded)
            {
                await _userManager.UpdateSecurityStampAsync(user);
            }

            TempData[result.Succeeded ? "SuccessMessage" : "ErrorMessage"] =
                result.Succeeded
                    ? isDisabled
                        ? $"{user.Email} can sign in again."
                        : $"{user.Email} can no longer sign in."
                    : JoinErrors(result);

            return RedirectToAction(nameof(Index));
        }

        private static string JoinErrors(IdentityResult result)
        {
            return string.Join(" ", result.Errors.Select(error => error.Description));
        }

        private static Guid? GetStaffMemberId(IEnumerable<Claim> claims)
        {
            var value = claims
                .FirstOrDefault(claim => claim.Type == AdminRoles.StaffMemberIdClaim)
                ?.Value;

            return Guid.TryParse(value, out var id)
                ? id
                : null;
        }

        private async Task<bool> IsProfileLinkedAsync(
            Guid staffMemberId,
            string? exceptUserId)
        {
            var users = await _userManager.Users
                .Where(user => user.Id != exceptUserId)
                .ToListAsync();

            foreach (var user in users)
            {
                var claims = await _userManager.GetClaimsAsync(user);

                if (GetStaffMemberId(claims) == staffMemberId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
