using System.Security.Cryptography;

namespace Rebel.Web.Services;

public static class ReservationCodeGenerator
{
    public static string Create() =>
        $"RR-{RandomNumberGenerator.GetHexString(10)}";
}
