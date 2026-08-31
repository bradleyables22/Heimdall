export function emitUnauthorized({
    response,
    emitLifecycle,
    sourceElement,
    detail
}) {
    if (Number(response?.status) !== 401 || typeof emitLifecycle !== "function")
        return true;

    return emitLifecycle(
        sourceElement || null,
        "heimdall:unauthorized",
        {
            ...detail,
            status: 401,
            response
        },
        { cancelable: true }
    );
}
