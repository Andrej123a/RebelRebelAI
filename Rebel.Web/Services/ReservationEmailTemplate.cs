using System.Net;

namespace Rebel.Web.Services
{
    public static class ReservationEmailTemplate
    {
        public static string BuildReceived(
            string fullName,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int numberOfGuests,
            string reservationCode,
            string logoUrl,
            string? eventTitle = null)
        {
            return BuildTemplate(
                fullName,
                reservationDate,
                reservationTime,
                numberOfGuests,
                reservationCode,
                logoUrl,
                eventTitle,
                badgeText: "REQUEST RECEIVED",
                badgeBackground: "#f4c430",
                title: "WE GOT YOUR REQUEST",
                message:
                    "Your reservation is waiting for staff approval. Keep your code handy so you can track or cancel the request.",
                closingTitle: "WE'LL CHECK THE FLOOR.",
                closingText: "You will hear from us once the team confirms it."
            );
        }

        public static string BuildApproved(
            string fullName,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int numberOfGuests,
            string reservationCode,
            string logoUrl,
            string? eventTitle = null)
        {
            return BuildTemplate(
                fullName,
                reservationDate,
                reservationTime,
                numberOfGuests,
                reservationCode,
                logoUrl,
                eventTitle,
                badgeText: "RESERVATION CONFIRMED",
                badgeBackground: "#f4c430",
                title: "YOUR TABLE IS LOCKED IN",
                message:
                    "The drinks are getting cold, the music is getting loud, and your table will be waiting.",
                closingTitle: "COME THIRSTY.",
                closingText: "Leave legendary."
            );
        }

        public static string BuildDeclined(
            string fullName,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int numberOfGuests,
            string reservationCode,
            string logoUrl,
            string? eventTitle = null)
        {
            return BuildTemplate(
                fullName,
                reservationDate,
                reservationTime,
                numberOfGuests,
                reservationCode,
                logoUrl,
                eventTitle,
                badgeText: "RESERVATION UPDATE",
                badgeBackground: "#c93c3c",
                title: "NOT THIS ROUND",
                message:
                    "Unfortunately, we couldn’t lock in your table for the selected date and time. Try another night — the Rebel vibe isn’t going anywhere.",
                closingTitle: "DON’T BE A STRANGER.",
                closingText: "There’s always another night waiting at Rebel."
            );
        }

        public static string BuildCancelled(
            string fullName,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int numberOfGuests,
            string reservationCode,
            string logoUrl,
            string? eventTitle = null)
        {
            return BuildTemplate(
                fullName,
                reservationDate,
                reservationTime,
                numberOfGuests,
                reservationCode,
                logoUrl,
                eventTitle,
                badgeText: "RESERVATION CANCELLED",
                badgeBackground: "#c93c3c",
                title: "YOUR RESERVATION WAS CANCELLED",
                message:
                    "This reservation has been cancelled. If that does not look right, contact the team directly.",
                closingTitle: "THANKS FOR UNDERSTANDING.",
                closingText: "We hope to see you another time."
            );
        }

        private static string BuildTemplate(
            string fullName,
            DateTime reservationDate,
            TimeSpan reservationTime,
            int numberOfGuests,
            string reservationCode,
            string logoUrl,
            string? eventTitle,
            string badgeText,
            string badgeBackground,
            string title,
            string message,
            string closingTitle,
            string closingText)
        {
            var safeName = WebUtility.HtmlEncode(fullName);
            var safeReservationCode = WebUtility.HtmlEncode(reservationCode);
            var safeLogoUrl = WebUtility.HtmlEncode(logoUrl);
            var safeEventTitle = WebUtility.HtmlEncode(eventTitle);

            var formattedDate = reservationDate.ToString("dddd, dd MMMM yyyy");
            var formattedTime = DateTime.Today
                .Add(reservationTime)
                .ToString("HH:mm");

            var logoHtml = string.IsNullOrWhiteSpace(logoUrl)
                ? """
                    <div style="font-size:26px; font-weight:900; color:#ffffff;">
                        REBEL REBEL
                    </div>

                    <div style="margin-top:5px; font-size:11px; font-weight:700; letter-spacing:2px; color:#f4c430;">
                        BY FAT KITCHEN
                    </div>
                  """
                : $"""
                    <img src="{safeLogoUrl}"
                         alt="Rebel Rebel by Fat Kitchen"
                         width="180"
                         style="display:block; width:180px; max-width:100%; height:auto; margin:0 auto; border:0;" />
                  """;

            var eventRow = string.IsNullOrWhiteSpace(eventTitle)
                ? string.Empty
                : $"""
                    <tr>
                        <td style="padding:17px 20px; border-top:1px solid #303030; color:#888888; font-size:12px; font-weight:700; letter-spacing:1px;">
                            EVENT
                        </td>

                        <td align="right"
                            style="padding:17px 20px; border-top:1px solid #303030; color:#f4c430; font-size:15px; font-weight:700;">
                            {safeEventTitle}
                        </td>
                    </tr>
                  """;

            return $"""
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="UTF-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <title>{title}</title>
                </head>

                <body style="margin:0; padding:0; background:#0c0c0c; font-family:Arial, Helvetica, sans-serif;">

                    <table role="presentation"
                           width="100%"
                           cellspacing="0"
                           cellpadding="0"
                           border="0"
                           style="width:100%; background:#0c0c0c;">

                        <tr>
                            <td align="center" style="padding:30px 14px;">

                                <table role="presentation"
                                       width="100%"
                                       cellspacing="0"
                                       cellpadding="0"
                                       border="0"
                                       style="width:100%; max-width:620px; background:#181818; border:1px solid #333333; border-radius:18px; overflow:hidden;">

                                    <tr>
                                        <td style="height:7px; background:#f4c430; font-size:0;">
                                            &nbsp;
                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:34px 25px 20px;">
                                            {logoHtml}
                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:0 25px 18px;">

                                            <span style="display:inline-block; padding:9px 15px; border-radius:30px; background:{badgeBackground}; color:#ffffff; font-size:11px; font-weight:800; letter-spacing:1.5px;">
                                                {badgeText}
                                            </span>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:0 30px;">

                                            <h1 style="margin:0; color:#ffffff; font-size:32px; line-height:1.15; font-weight:900;">
                                                {title}
                                            </h1>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:20px 38px 8px;">

                                            <p style="margin:0; color:#f4c430; font-size:17px; line-height:1.6; font-weight:700;">
                                                Hey {safeName},
                                            </p>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:0 38px 25px;">

                                            <p style="margin:0; color:#bdbdbd; font-size:15px; line-height:1.7;">
                                                {message}
                                            </p>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td style="padding:0 28px 28px;">

                                            <table role="presentation"
                                                   width="100%"
                                                   cellspacing="0"
                                                   cellpadding="0"
                                                   border="0"
                                                   style="width:100%; background:#101010; border:1px solid #303030; border-radius:14px;">

                                                <tr>
                                                    <td style="padding:17px 20px; border-bottom:1px solid #303030; color:#888888; font-size:12px; font-weight:700; letter-spacing:1px;">
                                                        CODE
                                                    </td>

                                                    <td align="right"
                                                        style="padding:17px 20px; border-bottom:1px solid #303030; color:#f4c430; font-size:16px; font-weight:900; letter-spacing:1px;">
                                                        {safeReservationCode}
                                                    </td>
                                                </tr>

                                                <tr>
                                                    <td style="padding:17px 20px; border-bottom:1px solid #303030; color:#888888; font-size:12px; font-weight:700; letter-spacing:1px;">
                                                        DATE
                                                    </td>

                                                    <td align="right"
                                                        style="padding:17px 20px; border-bottom:1px solid #303030; color:#ffffff; font-size:15px; font-weight:700;">
                                                        {formattedDate}
                                                    </td>
                                                </tr>

                                                <tr>
                                                    <td style="padding:17px 20px; border-bottom:1px solid #303030; color:#888888; font-size:12px; font-weight:700; letter-spacing:1px;">
                                                        TIME
                                                    </td>

                                                    <td align="right"
                                                        style="padding:17px 20px; border-bottom:1px solid #303030; color:#ffffff; font-size:15px; font-weight:700;">
                                                        {formattedTime}
                                                    </td>
                                                </tr>

                                                <tr>
                                                    <td style="padding:17px 20px; color:#888888; font-size:12px; font-weight:700; letter-spacing:1px;">
                                                        GUESTS
                                                    </td>

                                                    <td align="right"
                                                        style="padding:17px 20px; color:#ffffff; font-size:15px; font-weight:700;">
                                                        {numberOfGuests}
                                                    </td>
                                                </tr>

                                                {eventRow}

                                            </table>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:0 30px 12px;">

                                            <h2 style="margin:0; color:#f4c430; font-size:20px; font-weight:900;">
                                                {closingTitle}
                                            </h2>

                                            <p style="margin:7px 0 0; color:#ffffff; font-size:15px;">
                                                {closingText}
                                            </p>

                                        </td>
                                    </tr>

                                    <tr>
                                        <td align="center" style="padding:24px 30px 32px;">

                                            <p style="margin:0; color:#707070; font-size:12px; line-height:1.6;">
                                                Rebel Rebel by Fat Kitchen<br />
                                                Skopje, North Macedonia
                                            </p>

                                        </td>
                                    </tr>

                                </table>

                            </td>
                        </tr>

                    </table>

                </body>
                </html>
                """;
        }
    }
}
