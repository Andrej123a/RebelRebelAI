using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Rebel.Infrastructure.Data;
using Rebel.Web.Models;
using System.Diagnostics;
using System.Text;

namespace Rebel.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        private static readonly TimeZoneInfo SkopjeTimeZone =
            TimeZoneInfo.FindSystemTimeZoneById("Europe/Skopje");

        public HomeController(
            ILogger<HomeController> logger,
            AppDbContext context,
            IConfiguration configuration)
        {
            _logger = logger;
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            CancellationToken cancellationToken)
        {
            var skopjeNow = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                SkopjeTimeZone
            );

            var startOfTodayInSkopje = DateTime.SpecifyKind(
                skopjeNow.Date,
                DateTimeKind.Unspecified
            );

            var startOfTodayUtc = TimeZoneInfo.ConvertTimeToUtc(
                startOfTodayInSkopje,
                SkopjeTimeZone
            );

            var featuredEvent = await _context.Events
                .AsNoTracking()
                .Where(eventItem =>
                    eventItem.IsActive &&
                    eventItem.Date >= startOfTodayUtc
                )
                .OrderBy(eventItem => eventItem.Date)
                .ThenBy(eventItem => eventItem.StartTime)
                .FirstOrDefaultAsync(cancellationToken);

            var model = new HomeViewModel
            {
                FeaturedEvent = featuredEvent
            };

            return View(model);
        }

        [HttpGet]
        public IActionResult Contact()
        {
            return View();
        }

        [HttpGet("/sitemap.xml")]
        public IActionResult Sitemap()
        {
            var baseUrl =
                $"{Request.Scheme}://{Request.Host}";

            var paths = new List<string>
            {
                "/",
                "/Home/Menu",
                "/Events",
                "/Reservations/Create",
                "/Home/Contact"
            };

            if (!_configuration.GetValue<bool>("Presentation:HideAiFeatures"))
            {
                paths.Insert(2, "/RebelAI");
            }

            var sitemap = new StringBuilder();

            sitemap.AppendLine("""<?xml version="1.0" encoding="utf-8"?>""");
            sitemap.AppendLine("""<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">""");

            foreach (var path in paths)
            {
                sitemap.AppendLine("  <url>");
                sitemap.AppendLine($"    <loc>{baseUrl}{path}</loc>");
                sitemap.AppendLine("  </url>");
            }

            sitemap.AppendLine("</urlset>");

            return Content(
                sitemap.ToString(),
                "application/xml",
                Encoding.UTF8);
        }

        [HttpGet]
        public async Task<IActionResult> Menu(
            string section = "food",
            CancellationToken cancellationToken = default)
        {
            var products = await _context.Products
                .AsNoTracking()
                .Include(product => product.Category)
                .Where(product => !product.IsDeleted)
                .ToListAsync(cancellationToken);

            ViewBag.Section = section;

            return View(products);
        }

        [ResponseCache(
            Duration = 0,
            Location = ResponseCacheLocation.None,
            NoStore = true)]
        public IActionResult Error()
        {
            return View(
                new ErrorViewModel
                {
                    RequestId =
                        Activity.Current?.Id ??
                        HttpContext.TraceIdentifier
                }
            );
        }
    }
}
