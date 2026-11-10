// One backend per document. Configure endpoints and credentials before runtime boot.
export function getRequestCredentials(config) {
    const value = config.credentials ?? "same-origin";
    if (value !== "same-origin" && value !== "include")
        throw new TypeError('Heimdall.config.credentials must be "same-origin" or "include".');
    return value;
}

export function isAntiforgeryFailure(response) {
    return response.status === 400 &&
        response.headers.get("X-Heimdall-Antiforgery") === "invalid";
}
