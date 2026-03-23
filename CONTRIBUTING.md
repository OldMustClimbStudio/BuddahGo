# Contributing

## Branch Naming

- `feature/<topic>`
- `fix/<topic>`
- `chore/<topic>`
- `docs/<topic>`

## Commit Message Convention

Use conventional prefixes:

- `feat:` new functionality
- `fix:` bug fix
- `refactor:` internal code change without behavior change
- `chore:` maintenance updates
- `docs:` documentation updates

Examples:

- `feat: add steam lobby invite flow`
- `fix: avoid null transport during startup`

## Pull Request Convention

### PR Title
Use the same conventional prefix as commits:

```
feat: integrate Multiplayer Framework (Steam + FishyFacepunch + FishNet Demos)
```

### PR Description
Fill in the following sections in the description box:

```
## Summary
What this PR does and why.

## Test plan
- [ ] Step to verify change A
- [ ] Step to verify change B

## Affected areas
- `Assets/Scripts/` — what changed and why
- `Packages/manifest.json` — dependency updates

## Pending
Any known follow-up work left out of this PR.
```

### Merge Commit Message
Use GitHub's auto-generated message — do not edit it:

```
Merge pull request #N from feature/<topic>
```

The PR number links directly to the full description, commit list, and review history, making the auto-generated message sufficient.

## Pull Request Expectations

- Explain what changed and why
- List testing steps
- Mention impacted scenes or scripts
- Keep PR focused and reasonably small

## Unity-Specific Rules

- Always commit related `.meta` files
- Do not commit `Library/`, `Temp/`, `Logs/`, `UserSettings/`
- Avoid editing auto-generated `.csproj`/`.sln`
