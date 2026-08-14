namespace ControlDeck.Services;

internal static class AdBlockList
{
    private static readonly HashSet<string> BlockedDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "doubleclick.net",
        "googlesyndication.com",
        "googleadservices.com",
        "google-analytics.com",
        "googletagmanager.com",
        "googletagservices.com",
        "adservice.google.com",
        "adnxs.com",
        "adsrvr.org",
        "adform.net",
        "taboola.com",
        "outbrain.com",
        "criteo.com",
        "criteo.net",
        "scorecardresearch.com",
        "moatads.com",
        "moatpixel.com",
        "amazon-adsystem.com",
        "connect.facebook.net",
        "advertising.com",
        "rubiconproject.com",
        "pubmatic.com",
        "openx.net",
        "casalemedia.com",
        "3lift.com",
        "bidswitch.net",
        "yieldmo.com",
        "media.net",
        "quantserve.com",
        "adroll.com",
        "mathtag.com",
        "serving-sys.com",
        "flashtalking.com",
        "adsafeprotected.com",
        "doubleverify.com",
    };

    public static bool IsBlocked(string host) =>
        BlockedDomains.Any(domain => host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
                                      host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase));
}
