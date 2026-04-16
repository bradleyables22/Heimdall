# HeimdallFramework.Bootstrap

A strongly-typed, server-first Bootstrap helper library for building HTML with Heimdall and ASP.NET Core.

This library provides a typed abstraction over Bootstrap’s class system, eliminating stringly-typed CSS while preserving full control over markup and layout.

---

## Why This Exists

Traditional Bootstrap usage relies heavily on raw string classes:

```html
<div class="row mb-3 mt-3">
```

This works—but it is:

- error-prone (`mb3` vs `mb-3`)
- hard to refactor
- not discoverable
- not composable in a strongly typed system

HeimdallFramework.Bootstrap solves this by providing:

- strongly typed helpers
- IDE autocomplete for Bootstrap classes
- composable UI primitives
- seamless integration with server-rendered HTML

---

## Philosophy

This library follows the same principles as Heimdall:

- HTML-first – no abstraction over structure, only over classes
- Server-driven UI – works naturally with `IHtmlContent`
- Zero magic – everything maps directly to real Bootstrap classes
- Composable – small primitives, not giant components

This is not a component framework. It is a typed vocabulary for Bootstrap.

---

## Installation

```bash
dotnet add package HeimdallFramework.Bootstrap
```

---

## Usage

### Basic Example

```csharp
FluentHtml.Div(div =>
{
    div.Class(
        Bootstrap.Layout.Row,
        Bootstrap.Spacing.Mb(3),
        Bootstrap.Spacing.Mt(3)
    );

    div.Div(col =>
    {
        col.Class(Bootstrap.Layout.ColSpan(6, Bootstrap.Breakpoint.Md));

        col.P(p =>
        {
            p.Class(Bootstrap.Typography.Lead)
             .Text("Hello from Heimdall");
        });
    });
});
```

---

### Buttons

```csharp
button.Class(
    Bootstrap.Btn.Base,
    Bootstrap.Btn.Primary
);
```

---

### Tables

```csharp
table.Class(
    Bootstrap.Table.Base,
    Bootstrap.Table.Hover,
    Bootstrap.Table.Borderless
);
```

---

### Forms

```csharp
input.Class(Bootstrap.Form.Control);
```

---

### Layout Helpers

```csharp
Bootstrap.Layout.Row
Bootstrap.Layout.ColSpan(6, Bootstrap.Breakpoint.Md)
```

---

### Spacing Helpers

```csharp
Bootstrap.Spacing.Mb(3)
Bootstrap.Spacing.Mt(2)
Bootstrap.Spacing.P(4)
```

---

### Utility Helpers

```csharp
Bootstrap.Display.Flex
Bootstrap.Layout.JustifyContentBetween
Bootstrap.Layout.AlignItemsCenter
```

---

## Design Goals

### Strong Typing Without Abstraction Leakage

Every helper maps directly to a real Bootstrap class:

```csharp
Bootstrap.Spacing.Mb(3) → "mb-3"
```

No hidden behavior. No transformation layers.

---

### Discoverability

Instead of memorizing class names, use your IDE:

```csharp
Bootstrap.Spacing.
Bootstrap.Btn.
Bootstrap.Table.
```

---

### Composition Over Components

This library intentionally avoids large UI components.

Instead of:

- BootstrapCard.Create(...)

You build:

- Div + Class + Structure

This keeps you in control of markup and aligns with Heimdall’s philosophy.

---

## Integration with Heimdall

This library is designed to work seamlessly with:

- FluentHtml
- HeimdallHtml
- Content Actions & Invocations
- SSE-driven UI updates

Example:

```csharp
FluentHtml.Div(div =>
{
    div.Class(Bootstrap.Card.Base, Bootstrap.Spacing.Mb(3));

    div.Div(header =>
    {
        header.Class(Bootstrap.Card.Header)
              .Text("Live Data");
    });

    div.Div(body =>
    {
        body.Class(Bootstrap.Card.Body)
            .Heimdall()
            .Sse("metrics-stream", "metrics-body", HeimdallHtml.Swap.Inner);
    });
});
```

---

## What This Library Is NOT

- Not a component framework
- Not a replacement for Bootstrap
- Not a CSS abstraction layer
- Not tied to any frontend framework

---

## Versioning

This package follows semantic versioning:

- Major – breaking changes to class helpers or structure
- Minor – new helpers or Bootstrap coverage
- Patch – fixes and improvements

---

## Roadmap

- Expand Bootstrap coverage
- Add more utility helpers
- Improve documentation examples
- Provide optional higher-level patterns (kept separate from core)

---

## License

MIT
