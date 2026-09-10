# UnityEngine stubs — a compile check, not a simulator

**These types are fake, and they are never shipped.** They live outside
`Assets/` precisely so Unity never sees them.

## What this is for

Unity is not installed on this machine and there is no disk space to install it,
so `Assets/Scripts/Unity/` — twelve files of MonoBehaviours — could not be
compiled at all. Writing that much C# with no compiler is how you ship a file
with a misspelled method name in it.

These stubs declare just enough of the UnityEngine surface for
`dotnet build` to typecheck the MonoBehaviour layer.

## What it proves, and what it does not

**Caught by this:** typos, wrong member names between my own classes, missing
`using` directives, type errors against `CyberGoal.Core`, wrong argument counts,
unreachable or ambiguous code — the great majority of what actually goes wrong
across a dozen new files.

**NOT caught by this:** whether the real Unity API matches. These signatures
encode my belief about Unity, so where I have misremembered one, the stub
faithfully reproduces the mistake and compiles happily. Anything to do with
runtime behaviour — shader names resolving, `Resources.GetBuiltinResource`
finding a font, URP property names, execution order — is equally invisible.

Treat a green build here as "internally consistent", never as "verified".
The Unity layer stays **UNVERIFIED** until it is opened in an editor.
