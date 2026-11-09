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
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Web/Heimdall.Web.csproj') --configuration Release --output $feed --nologo
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Functions/Heimdall.Functions.csproj') --configuration Release --output $feed --nologo
Invoke-DotNet pack (Join-Path $repository 'Heimdall.Server/Heimdall.Server.csproj') --configuration Release --no-build --output $feed --nologo
Invoke-DotNet pack (Join-Path $TemplateRepository 'HeimdallTemplatePacker/HeimdallTemplatePacker.csproj') --configuration Release --output $templateOutput --nologo
[xml]$project = Get-Content (Join-Path $repository 'Heimdall.Functions/Heimdall.Functions.csproj') -Raw
$version = [string]$project.Project.PropertyGroup.Version
$package = Join-Path $templateOutput "HeimdallFramework.Templates.$version.nupkg"
if (-not (Test-Path -LiteralPath $package)) { throw "Template and Functions preview versions must match: $package" }
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
foreach ($variant in @('NoAuth', 'Oidc')) {
    $authentication = if ($variant -eq 'Oidc') { 'oidc' } else { 'none' }
    $directory = Join-Path $validation $variant
    Invoke-DotNet new heimdall-functions --authentication $authentication -n "Functions$variant" -o $directory --debug:custom-hive $hive
    $generatedProject = Join-Path $directory "Functions$variant.csproj"
    if ($variant -eq 'NoAuth' -and ((Test-Path (Join-Path $directory 'Authentication')) -or ((Get-Content $generatedProject -Raw) -match 'OpenIdConnect'))) {
        throw 'No-auth generation unexpectedly included authentication code or dependencies.'
    }
    # Fresh packages prevent reuse of an older unpublished build with the same version.
    Invoke-DotNet restore $generatedProject --configfile $config --packages $packages --nologo
    Invoke-DotNet build $generatedProject --no-restore --warnaserror --nologo
    if (-not (Test-Path -LiteralPath (Join-Path $directory "bin/Debug/net10.0/wwwroot/site.css"))) {
        throw "$variant build output is missing the application web-root asset."
    }
    $metadata = @(Get-Content (Join-Path $directory 'bin/Debug/net10.0/functions.metadata') -Raw | ConvertFrom-Json)
    if ($metadata.Count -ne 1 -or $metadata[0].name -ne 'HeimdallHttp') {
        throw "$variant must contain one generated HTTP trigger for pages, protocol, assets, and authentication."
    }
    $results[$variant] = $directory
}
$publish = Join-Path $validation 'publish'
Invoke-DotNet publish (Join-Path $results.NoAuth 'FunctionsNoAuth.csproj') --no-restore --output $publish --nologo
foreach ($relative in @('wwwroot/site.css', 'wwwroot/_content/HeimdallFramework.Web/heimdall-bundle.min.js', 'functions.metadata', 'FunctionsNoAuth.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $publish $relative))) { throw "Published starter is missing $relative" }
}
$publishedHost = Get-Content (Join-Path $publish 'host.json') -Raw | ConvertFrom-Json
if ($publishedHost.extensions.http.routePrefix -ne '') { throw 'Published starter lost its empty HTTP route prefix.' }
$results.Version = $version
$results.TemplatePackage = $package
$results.PublishDirectory = $publish
$manifest = Join-Path $repository 'artifacts/functions/starters.json'
$results | ConvertTo-Json | Set-Content -LiteralPath $manifest
Write-Output "Generated, built, and publish-checked both starters. Manifest: $manifest"
