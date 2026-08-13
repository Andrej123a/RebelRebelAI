using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Rebel.Web.Authorization;

namespace Rebel.Web.Hubs
{
    [Authorize(Policy = AdminPolicies.Backstage)]
    public class NotificationHub : Hub
    {
    }
}
