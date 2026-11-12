# RemoteDeployTool

Deploys what a release drop holds.

A [Bitenovac.RemoteBuildTool](https://www.nuget.org/packages/Bitenovac.RemoteBuildTool) release
drops its apps and packages into a directory of its own, named by the release, such as
`2026.10.07.001`, and writes a `manifest.json` there that lists each app and package with a
SHA-256 of its content. RemoteDeployTool reads those manifests.

It says which drops there are, what one holds, and what changed between two, and pushes a drop's
packages to a NuGet feed.

```
remotedeploytool drops                  every drop, newest first
remotedeploytool show [drop]            what a drop holds; the newest when none is named
remotedeploytool changes <from> [to]    what differs from one drop to another, the newest by default
remotedeploytool nuget <package>... [--drop <drop>] [--same-version]
remotedeploytool pages <app> <site> [--drop <drop>]
```

`changes` goes by the fingerprint RemoteBuildTool gives each project, which changes only when what
the project is built from does, so it lists what a deploy of the newer drop would have to deploy.

## Pushing packages

`nuget` pushes the packages named by id, or by a prefix ending in `.*`, from the newest drop or the
one `--drop` names. It checks them all before it pushes any:

- A version that starts with a year is the drop's date, given to a project with no version of its
  own. A feed never deletes a version, and that one would outrank every real one, so it is refused.
- With `--same-version`, the packages must share one version, as a tool and its service must.

A package the feed already has is skipped, so a push that stopped part way can be run again. A push
in which the feed already has every package fails, so a version that was not raised fails loudly.

## Preparing a GitHub Pages site

`pages` writes a drop's published web app into an empty or missing directory, ready to upload as a
GitHub Pages site, for a site served from the root of its domain:

- The app's `wwwroot` becomes the site, without the precompressed `.br` and `.gz` copies: Pages
  compresses on its own and would serve them only as downloads.
- `404.html` is a copy of `index.html`, so a reloaded deep link reaches the app, which routes it.
- `.nojekyll` stops Pages running Jekyll, which would drop `_framework`.

## Environment

| Variable | Default | Meaning |
|---|---|---|
| `REMOTEDEPLOYTOOL_DROP_ROOT` | `artifacts/official` | Where the drops are. |
| `REMOTEDEPLOYTOOL_NUGET_SOURCE` | nuget.org | Where packages are pushed: a feed URL or a directory. |
| `NUGET_API_KEY` | none | The feed's key; required for a feed on the web. |

## License

MIT. Source and issues:
[github.com/DarkVader0/Bitenovac](https://github.com/DarkVader0/Bitenovac/tree/master/src/RemoteDeployTool/Bitenovac.RemoteDeployTool).
