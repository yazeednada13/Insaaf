# Insaaf — Founder-Controlled Git Workflow

**Version:** 1.0  
**Last updated:** 2026-08-26  
**ADR:** [ADR-010](decisions.md#adr-010-founder-controlled-git)

The **founder** performs all Git operations manually. The **Lead Agent** advises, explains, and recommends checkpoints but **never modifies repository state**.

---

## 1. Strict Git execution rule

The Lead Agent MUST NOT execute any Git operation that changes repository state.

**Never execute:**

- `git add`, `commit`, `push`, `pull`, `merge`, `rebase`, `checkout`, `switch`, `reset`, `stash`, `clone`
- Creating or deleting branches
- Modifying remotes
- Rewriting repository history

### Read-only inspection is allowed

The agent MAY inspect Git state when necessary for planning or guidance:

- `git status`
- `git branch`
- `git log`
- `git remote -v`
- `git diff`
- `git diff --stat`

These are for inspection only. If Git state is already known from the conversation, prefer that instead of unnecessary inspection.

---

## 2. Lead Agent Git responsibility

Before starting a new feature or meaningful task, tell the founder:

1. Which branch should be used
2. Whether a new `feature/*` branch is needed
3. The recommended branch name
4. Whether the working tree should be clean
5. Why this Git state is appropriate

Example:

```text
### Git Checkpoint

Recommended branch:
feature/backend-foundation

Please create and switch to this branch manually before implementation.

I will not execute the Git command.
```

---

## 3. Git workflow

```text
main
  ↑
develop
  ↑
feature/*
```

### Branch responsibilities

| Branch | Purpose |
|--------|---------|
| `main` | Production-ready code |
| `develop` | Integration branch |
| `feature/*` | Individual feature or coherent implementation slice |

Normal flow:

```text
feature/* → develop → main
```

- Do not work directly on `main` for feature development
- Do not create unnecessary branches
- Prefer one feature branch per coherent slice

### Example branch names

```text
feature/backend-foundation
feature/authentication
feature/restaurants
feature/restaurant-discovery
feature/reviews-read
feature/qr-verification
feature/social-core
feature/social-feed
feature/profiles
```

Branch names should be short, descriptive, and related to the implementation slice.

---

## 4. Before implementation

1. Inspect repository state if necessary (read-only)
2. Determine the appropriate branch
3. Tell the founder whether a new feature branch should be created
4. Do not create or switch branches yourself
5. Wait for the founder when a new branch is needed

Example:

```text
### Git Checkpoint

Current branch: develop

The next task is a new implementation slice.

Recommended branch:
feature/backend-foundation

Why:
This keeps Phase 1 backend work isolated from the integration branch.

Next Git action:
Create and switch to the recommended feature branch manually.

I will not execute this operation.
```

---

## 5. During implementation

After the founder confirms Git state:

- Work normally
- Do not commit, push, or merge automatically
- After a meaningful logical unit, provide a Git checkpoint

Meaningful checkpoint examples:

- A complete task is implemented
- A coherent feature slice is implemented
- Tests for a logical unit are complete
- Documentation for a logical change is complete
- A safe restore point before the next task

Do not recommend a commit after every trivial file change.

---

## 6. Git Checkpoint format

```text
### Git Checkpoint

Status: Ready for commit | Ready for push | Ready for PR

Branch:
feature/backend-foundation

Changes:
- Created backend solution structure
- Added project references
- Configured initial dependency direction

Recommended commit:
feat: establish backend solution structure

Why now?
This task forms one complete logical unit and is a good restore point before continuing.

Next Git action:
1. Review the changes.
2. Run the Git commands manually.
3. Commit using the recommended message.
4. Push the feature branch when appropriate.

I will not execute these Git operations.
```

---

## 7. Commit strategy

Use **Conventional Commits**:

```text
type: short description
```

Allowed types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`

**Good examples:**

```text
feat: establish backend solution structure
feat: add API health endpoint
feat: configure EF Core database context
feat: implement restaurant repository
feat: add restaurant discovery endpoint
fix: handle invalid restaurant identifier
refactor: simplify restaurant query
test: add restaurant filtering tests
docs: update implementation progress
```

**Avoid:**

```text
update, changes, fix, stuff, final, final2, work, test
```

Keep commits small, meaningful, logically grouped, and easy to revert. Do not combine unrelated changes.

---

## 8. Push guidance

After a meaningful commit, recommend push when appropriate:

```text
### Git Checkpoint

Status: Ready for push

The feature branch contains a completed logical checkpoint.

Recommended action:
Push the current feature branch to GitHub.

I will not execute the push.
```

Never claim changes are on GitHub unless the founder confirms push succeeded.

---

## 9. Pull Request workflow

When a feature branch is complete and verified:

**Target:** `feature/*` → `develop`

The founder creates and manages the PR manually.

The agent provides:

1. Recommended PR title
2. Target branch
3. Short PR description
4. Review checklist
5. Known risks or remaining issues

Example:

```text
### Pull Request Ready

Source:
feature/backend-foundation

Target:
develop

Suggested title:
feat: establish backend foundation

Checklist:
- [ ] Build passes
- [ ] Tests pass
- [ ] Architecture rules respected
- [ ] No secrets committed
- [ ] Documentation updated
- [ ] Figma requirements respected where applicable
```

The agent never creates, approves, or merges PRs.

See also: [.github/PULL_REQUEST_TEMPLATE.md](../.github/PULL_REQUEST_TEMPLATE.md)

---

## 10. Release flow

```text
feature/*
    ↓
develop
    ↓
main
```

- `feature/*` → `develop` for completed features
- `develop` → `main` when integration is stable and production-ready

Do not merge or release automatically.

---

## 11. Git state must never be confused

```text
Files changed locally
        ↓
Committed locally
        ↓
Pushed to GitHub
        ↓
Pull Request opened
        ↓
Pull Request merged
```

Never assume one state implies the next:

- Local changes are not committed
- A commit is not pushed
- A push does not mean a PR exists
- A PR is not merged
- Merge into `develop` does not mean code is in `main`

Confirm with the founder or read-only inspection.

---

## 12. Learning requirement

When recommending a Git action, briefly explain **why**.

```text
Why now?

This task is complete and logically independent from the next task.
Committing now creates a safe restore point before we continue.
```

Keep explanations short unless the founder asks for detail.

---

## 13. Relationship with Insaaf architecture

Before recommending a branch or commit, consider:

- [architecture.md](architecture.md)
- [product-spec.md](product-spec.md)
- [implementation-plan.md](implementation-plan.md)
- [project-progress.md](project-progress.md)
- [decisions.md](decisions.md)
- Applicable `.cursor/rules/*.mdc`

Do not introduce Git workflow changes that conflict with approved architecture without informing the founder.

---

## 14. Documentation maintenance

Major Git workflow decisions are recorded in [decisions.md](decisions.md) (ADR-010).

Update this file when the founder-approved Git workflow changes.

---

## 15. Founder vs agent ownership

| Founder owns | Lead Agent owns |
|--------------|-----------------|
| Repository history | Planning and orchestration |
| Branches | Architecture consistency |
| Commits | Git guidance and checkpoints |
| Pushes | Commit message recommendations |
| Pull Requests | PR recommendations |
| Merges and releases | Brief WHY explanations |

The Lead Agent is an **advisor and orchestrator**, not the Git operator.

---

## 16. Implementation loop

```text
Plan
  ↓
Founder approval
  ↓
Implement
  ↓
Verify
  ↓
Git Checkpoint
  ↓
Founder performs Git operation
  ↓
Continue
```

Never autonomously modify Git state.

---

## Related documents

- [architecture.md](architecture.md)
- [implementation-plan.md](implementation-plan.md)
- [project-progress.md](project-progress.md)
- [.cursor/rules/git.mdc](../.cursor/rules/git.mdc)
