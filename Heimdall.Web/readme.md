# HeimdallFramework.Web

`HeimdallFramework.Web` distributes the [Heimdall](https://heimdall-framework.org) browser runtime as Razor Class Library static assets.

This package is the client-side half of Heimdall. It applies server-rendered updates in the browser and contains no ASP.NET Core middleware or server action pipeline. Interactive applications normally pair it with `HeimdallFramework.Server`.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for installation, markup, browser behavior, configuration, events, security, and compatibility. This package README intentionally avoids maintaining a second copy of the runtime documentation.

## Install

```bash
dotnet add package HeimdallFramework.Web
```

Use the package through its Razor Class Library asset. See [the current documentation](https://heimdall-framework.org) for the supported asset reference and application setup.

## Links

- [Documentation](https://heimdall-framework.org)
- [Source and issue tracker](https://github.com/bradleyables22/Heimdall)
- [NuGet package](https://www.nuget.org/packages/HeimdallFramework.Web)
- [MIT license](https://github.com/bradleyables22/Heimdall/blob/master/LICENSE)
