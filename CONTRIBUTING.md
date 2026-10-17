# Contributing to WinSpeed

Thanks for helping. Bug reports, ideas, documentation fixes and code are all welcome.

## Reporting bugs and suggesting features

Use the [issue templates](https://github.com/anand34577/winspeed/issues/new/choose). For bugs, include your WinSpeed and Windows versions and, if there is one, `error.log` from the data folder (About → Open data folder).

## Improving the user guide

The [wiki](https://github.com/anand34577/winspeed/wiki) is generated from the [`wiki/`](wiki) folder, so edit the Markdown there and open a pull request. Changes are published automatically when merged.

## Working on the code

1. Install the [.NET SDK](https://dotnet.microsoft.com/download) 8 or newer (and optionally [Inno Setup 6](https://jrsoftware.org/isdl.php)).
2. Build: `./build.ps1`, or `dotnet build -c Release` for a quick build.
3. Put an empty file named `portable` next to `bin\Release\net462\WinSpeed.exe` so test runs keep their data in `bin\Release\net462\Data`.

See [Building from source](https://github.com/anand34577/winspeed/wiki/Building-from-Source) for the project layout.

### Guidelines

- Keep it light: no new runtime dependencies, and no work on the UI thread that can block.
- Match the surrounding code style (`.editorconfig` is included).
- Every new setting needs a control in the app and a row in [Settings reference](wiki/Settings-Reference.md).
- Check both light and dark themes, and 100 % and 150 % display scaling, for UI changes.
- Add a line under **Unreleased** in [CHANGELOG.md](CHANGELOG.md) for anything users will notice.
- One topic per pull request, with a short description of what you changed and how you tested it.

By contributing you agree that your contribution is licensed under the [MIT License](LICENSE).
