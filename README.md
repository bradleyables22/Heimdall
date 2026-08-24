# Heimdall

[![Heimdall Server on NuGet](https://img.shields.io/nuget/v/HeimdallFramework.Server?label=HeimdallFramework.Server)](https://www.nuget.org/packages/HeimdallFramework.Server)
[![NuGet downloads](https://img.shields.io/nuget/dt/HeimdallFramework.Server?label=server%20downloads)](https://www.nuget.org/packages/HeimdallFramework.Server)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)

Heimdall is an HTML-first framework for building interactive ASP.NET Core applications while keeping the server in control of the UI.

```text
browser event -> ASP.NET action -> HTML response -> targeted DOM update
```

Pages render as normal documents, interactions invoke server-side actions, and responses return HTML fragments for the browser to apply. Heimdall also supports server-rendered HTML helpers, real-time updates, MVC integration, and static site generation.

> [heimdall-framework.org](https://heimdall-framework.org) is the source of truth for setup, concepts, API usage, examples, configuration, security, and deployment guidance. This README intentionally stays high level so it does not compete with the documentation.

## Packages

| Package | Purpose |
| --- | --- |
| [`HeimdallFramework.Server`](https://www.nuget.org/packages/HeimdallFramework.Server) | ASP.NET Core server runtime and rendering support |
| [`HeimdallFramework.Web`](https://www.nuget.org/packages/HeimdallFramework.Web) | Browser runtime distributed as Razor Class Library assets |
| [`HeimdallFramework.Bootstrap`](https://www.nuget.org/packages/HeimdallFramework.Bootstrap) | Optional strongly typed Bootstrap class helpers |

Most interactive applications use both the Server and Web packages:

```bash
dotnet add package HeimdallFramework.Server
dotnet add package HeimdallFramework.Web
```

For the current getting-started instructions and supported patterns, visit [the Heimdall documentation](https://heimdall-framework.org).

## Development

Run the .NET test suite from the repository root:

```bash
dotnet test Heimdall.slnx
```

Verify the browser runtime and package contents:

```bash
cd Heimdall.Web
npm ci
npm run verify:all
```

Run the full browser-level suite:

```bash
cd Heimdall.E2E
npm ci
npm test
```

Issues and implementation feedback are welcome in the [GitHub repository](https://github.com/bradleyables22/Heimdall).

## License

Heimdall is available under the [MIT License](LICENSE).
