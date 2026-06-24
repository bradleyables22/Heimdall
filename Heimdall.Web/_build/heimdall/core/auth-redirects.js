function getCurrentPageReturnUrl(global) {
    const location = global.location;
    if (!location)
        return "/";

    return `${location.pathname || "/"}${location.search || ""}${location.hash || ""}`;
}

function getMatchingSearchParamNames(searchParams, name) {
    const lowerName = String(name || "").toLowerCase();
    const matches = [];

    for (const key of searchParams.keys()) {
        if (String(key || "").toLowerCase() === lowerName && !matches.includes(key))
            matches.push(key);
    }

    return matches;
}

export function normalizeFollowedAuthRedirectUrl(global, getConfig, redirectUrl) {
    const config = typeof getConfig === "function"
        ? (getConfig() || {})
        : {};
    const returnUrlParameter = config.authReturnUrlParameter || "ReturnUrl";

    if (!returnUrlParameter)
        return redirectUrl;

    try {
        const nextUrl = new URL(redirectUrl, global.location?.origin || undefined);
        const matchingNames = getMatchingSearchParamNames(nextUrl.searchParams, returnUrlParameter);
        if (matchingNames.length === 0)
            return nextUrl.toString();

        for (const name of matchingNames)
            nextUrl.searchParams.set(name, getCurrentPageReturnUrl(global));

        return nextUrl.toString();
    } catch {
        return redirectUrl;
    }
}

export function getAuthRedirectUrlFromResponse(response) {
    if (!response)
        return null;

    if (response.redirected && response.url)
        return response.url;

    const status = Number(response.status);
    if (status !== 401 && status !== 403)
        return null;

    try {
        return response.headers && typeof response.headers.get === "function"
            ? response.headers.get("Location")
            : null;
    } catch {
        return null;
    }
}
