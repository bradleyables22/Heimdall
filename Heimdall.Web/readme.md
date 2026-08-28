# HeimdallFramework.Web

`HeimdallFramework.Web` distributes the [Heimdall](https://heimdall-framework.org) browser runtime as Razor Class Library static assets.

This package is the client-side half of Heimdall. It applies server-rendered updates in the browser and contains no ASP.NET Core middleware or server action pipeline. Interactive applications normally pair it with `HeimdallFramework.Server`.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for installation, markup, browser behavior, configuration, events, security, and compatibility. This package README intentionally avoids maintaining a second copy of the runtime documentation.

## Install

```bash
dotnet add package HeimdallFramework.Web
```

Use the package through its Razor Class Library asset. See [the current documentation](https://heimdall-framework.org) for the supported asset reference and application setup.

Forms containing file inputs are submitted as `multipart/form-data`; other declarative form payloads remain JSON. `Heimdall.invoke` also accepts a browser `FormData` instance for programmatic uploads.

Elements rendered by the Server package's fluent `.LocalizeTime(...)` helper are converted to the browser user's local timezone entirely on the client. Initial document content is processed during startup; content returned by actions, out-of-band invocations, and Bifrost is formatted before insertion. Dynamically added elements and changes to `heimdall-time`, `heimdall-time-format`, or inherited `lang` values are handled by the runtime observer.

Applications can customize or observe formatting through the cancellable `heimdall:time-before` event and the `heimdall:time-after` and `heimdall:time-error` events. Formatted output is always assigned as text, and formatting failures preserve the server fallback.

## Links

- [Documentation](https://heimdall-framework.org)
- [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Web)
- [MIT license](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
