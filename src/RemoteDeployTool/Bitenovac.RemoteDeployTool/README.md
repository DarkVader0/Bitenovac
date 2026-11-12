# RemoteDeployTool

Deploys what a release drop holds.

A [Bitenovac.RemoteBuildTool](https://www.nuget.org/packages/Bitenovac.RemoteBuildTool) release
drops its apps and packages into a directory of its own, named by the release, such as
`2026.10.07.001`, and writes a `manifest.json` there that lists each app and package with a
SHA-256 of its content. RemoteDeployTool reads those manifests.

This release reads only: it says which drops there are, what one holds, and what changed between
two. Deploying comes next.

```
remotedeploytool drops                  every drop, newest first
remotedeploytool show [drop]            what a drop holds; the newest when none is named
remotedeploytool changes <from> [to]    what differs from one drop to another, the newest by default
```

Two drops hold the same artifact exactly when its SHA-256 matches, so `changes` lists what a
deploy of the newer drop would have to deploy.

## Environment

| Variable | Default | Meaning |
|---|---|---|
| `REMOTEDEPLOYTOOL_DROP_ROOT` | `artifacts/official` | Where the drops are. |

## License

MIT. Source and issues:
[github.com/DarkVader0/Bitenovac](https://github.com/DarkVader0/Bitenovac/tree/master/src/RemoteDeployTool/Bitenovac.RemoteDeployTool).
