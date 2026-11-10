param([string]$TemplateRepository)
$ErrorActionPreference = 'Stop'
$repository = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (-not $TemplateRepository) { $TemplateRepository = [System.IO.Path]::GetFullPath((Join-Path $repository '../Heimdall-Template-App')) }
function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}
$feed = Join-Path $repository 'artifacts/functions/packages'
$templateOutput = Join-Path $TemplateRepository 'artifacts/functions-preview'
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Web/Heimdall.Web.csproj') --configuration Release --output $feed --nologo '-m:1'
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Functions/Heimdall.Functions.csproj') --configuration Release --output $feed --nologo '-m:1'
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Rendering/Heimdall.Rendering.csproj') --configuration Release --no-build --output $feed --nologo
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Server/Heimdall.Server.csproj') --configuration Release --no-build --output $feed --nologo
Invoke-DotNet pack (Join-Path $TemplateRepository 'HeimdallTemplatePacker/HeimdallTemplatePacker.csproj') --configuration Release --output $templateOutput --nologo
[xml]$project = Get-Content (Join-Path $repository 'Heimdall.Functions/Heimdall.Functions.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
[xml]$templateProject = Get-Content (Join-Path $TemplateRepository 'HeimdallTemplatePacker/HeimdallTemplatePacker.csproj') -Raw
$templateVersion = [string]$templateProject.Project.PropertyGroup.Version
$package = Join-Path $templateOutput "HeimdallFramework.Templates.$templateVersion.nupkg"
if (-not (Test-Path -LiteralPath $package)) { throw "Missing template package: $package" }
$runId = [Guid]::NewGuid().ToString('N')
$validation = Join-Path $repository "tmp/functions-starter-validation/$runId"
$hive = Join-Path $validation 'hive'
$packages = Join-Path $validation 'packages'
New-Item -ItemType Directory -Path $validation -Force | Out-Null
$config = Join-Path $validation 'NuGet.Config'
$escapedFeed = [System.Security.SecurityElement]::Escape($feed)
[System.IO.File]::WriteAllText($config, "<configuration><packageSources><clear/><add key=`"preview`" value=`"$escapedFeed`"/><add key=`"nuget.org`" value=`"https://api.nuget.org/v3/index.json`"/></packageSources></configuration>")
Invoke-DotNet new install $package --debug:custom-hive $hive
$results = @{}
foreach ($variant in @('NoAuth')) {
    $directory = Join-Path $validation $variant
    Invoke-DotNet new heimdall-functions -n "Functions$variant" -o $directory --debug:custom-hive $hive
    $generatedProject = Join-Path $directory "Functions$variant.csproj"
    if ($variant -eq 'NoAuth' -and ((Test-Path (Join-Path $directory 'Authentication')) -or ((Get-Content $generatedProject -Raw) -match 'OpenIdConnect'))) {
        throw 'No-auth generation unexpectedly included authentication code or dependencies.'
    }
    # Fresh packages prevent reuse of an older unpublished build with the same version.
    Invoke-DotNet restore $generatedProject --configfile $config --packages $packages --nologo
    Invoke-DotNet build $generatedProject --no-restore --warnaserror --nologo '-m:1'
    if (Test-Path (Join-Path $directory 'UiFunctions.cs')) { throw 'The starter must not expose a Function class.' }
    $items = (& dotnet msbuild $generatedProject -getItem:None | Out-String | ConvertFrom-Json).Items.None
    foreach ($name in @('host.json', 'local.settings.json')) {
        if (@($items | Where-Object Identity -eq $name).Count -ne 1) { throw "Expected exactly one visible None item for $name" }
        if (-not (Test-Path (Join-Path $directory "bin/Debug/net10.0/$name"))) { throw "Missing build output: $name" }
    }
    if (-not (Test-Path -LiteralPath (Join-Path $directory "bin/Debug/net10.0/wwwroot/css/bootstrap.css"))) {
        throw "$variant build output is missing the application web-root asset."
    }
    $metadata = @(Get-Content (Join-Path $directory 'bin/Debug/net10.0/functions.metadata') -Raw | ConvertFrom-Json)
    if ($metadata.Count -ne 1 -or $metadata[0].name -ne 'HeimdallHttp' -or $metadata[0].entryPoint -ne 'Heimdall.Functions.Generated.HeimdallHttp.Run') {
        throw "$variant must contain one generated HTTP trigger for pages, protocol, assets, and authentication."
    }
    $results[$variant] = $directory
    $previousBlob = $env:DataProtection__BlobUri
    $previousDirectory = $env:DataProtection__KeyDirectory
    try {
        $env:DataProtection__BlobUri = 'http://invalid.example/keys.xml'
        $env:DataProtection__KeyDirectory = $null
        $startup = & dotnet (Join-Path $directory "bin/Debug/net10.0/Functions$variant.dll") 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $startup -notmatch 'must be an HTTPS blob URL') {
            throw 'Invalid BlobUri must fail before worker startup.'
        }
        $env:DataProtection__BlobUri = 'https://example.blob.core.windows.net/keys/app.xml'
        $env:DataProtection__KeyDirectory = 'conflicting-directory'
        $startup = & dotnet (Join-Path $directory "bin/Debug/net10.0/Functions$variant.dll") 2>&1 | Out-String
        if ($LASTEXITCODE -eq 0 -or $startup -notmatch 'not both') {
            throw 'Conflicting key stores must fail before worker startup.'
        }
    }
    finally {
        $env:DataProtection__BlobUri = $previousBlob
        $env:DataProtection__KeyDirectory = $previousDirectory
    }
}
$publish = Join-Path $validation 'publish'
Invoke-DotNet publish (Join-Path $results.NoAuth 'FunctionsNoAuth.csproj') --no-restore --output $publish --nologo '-m:1'
if (Test-Path (Join-Path $publish 'local.settings.json')) { throw 'Local settings must never be published.' }
foreach ($relative in @('wwwroot/css/bootstrap.css', 'wwwroot/js/bootstrap-bundle.js', 'wwwroot/_content/HeimdallFramework.Web/heimdall-bundle.min.js', 'functions.metadata', 'FunctionsNoAuth.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $relative))) { throw "Published starter is missing $relative" }
}
$publishedHost = Get-Content (Join-Path $publish 'host.json') -Raw | ConvertFrom-Json
if ($publishedHost.extensions.http.routePrefix -ne '') { throw 'Published starter lost its empty HTTP route prefix.' }
$results.Version = $version
$results.TemplatePackage = $package
$results.PublishDirectory = $publish
$manifest = Join-Path $repository 'artifacts/functions/starters.json'
$results | ConvertTo-Json | Set-Content -LiteralPath $manifest
Write-Output "Generated, built, and publish-checked the Functions starter. Manifest: $manifest"
