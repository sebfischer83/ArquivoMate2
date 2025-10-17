# GitHub Actions CI/CD Overview

## Current Workflows

### 1. **ci.yml** - Backend (.NET) CI Pipeline
**Trigger:** Push to `master` and PRs (excludes UI paths)

**What it does:**
- ✅ Setup .NET 9.0
- ✅ Restore NuGet packages (with caching)
- ✅ Build solution
- ✅ Run tests with XPlat Code Coverage
- ✅ Generate coverage report (HTML, Cobertura, Markdown)
- ✅ Post test results as GitHub PR comment
- ✅ Display test report visualization

**Path exclusions:**
```yaml
paths-ignore:
  - 'src/ArquivoMate2.Ui/**'    # Skips UI changes
  - 'docs/**'
  - '**/*.md'
```

---

### 2. **ui-ci.yml** - Frontend (Angular) CI Pipeline
**Trigger:** Push to `src/ArquivoMate2.Ui/**` and PRs

**What it does:**
- ✅ Setup Node.js 20
- ✅ Install dependencies (with npm cache)
- ✅ Run lint (if defined)
- ✅ Build production bundle
- ⚠️ **Currently does NOT:**
  - ❌ Run tests
  - ❌ Publish Docker image
  - ❌ Deploy anywhere

**Current file:** `.github/workflows/ui-ci.yml`

---

### 3. **publish-docker.yml** - Multi-Architecture Docker Publishing
**Trigger:** Git tags matching `v*` or manual workflow_dispatch

**What it does:**
- ✅ Parse version from Git tag or manual input
- ✅ Setup QEMU for multi-arch builds
- ✅ Setup Docker Buildx
- ✅ Login to Docker Hub
- ✅ Build multi-arch image (linux/amd64, linux/arm64)
- ✅ Push to Docker Hub: `sebfischer83/arquivomate2:{version}`
- ✅ Publish .NET artifacts

**Current focus:** Backend API (`src/ArquivoMate2.API/Dockerfile`)

**Example:**
```bash
# Tag and push
git tag v1.0.0
git push origin v1.0.0

# Triggers publish-docker.yml
# Builds and publishes: sebfischer83/arquivomate2:v1.0.0
```

---

## Architecture: Backend vs Frontend Publishing

```
┌─────────────────────────────────────────────────────┐
│         GitHub Repository (ArquivoMate2)            │
└─────────────────────────────────────────────────────┘
                          │
        ┌─────────────────┼─────────────────┐
        │                 │                 │
        ▼                 ▼                 ▼
   ┌────────────┐  ┌────────────┐  ┌──────────────┐
   │  ci.yml    │  │ ui-ci.yml  │  │publish-docker│
   │ (.NET)     │  │ (Angular)  │  │   (Multi)    │
   └────────────┘  └────────────┘  └──────────────┘
        │                │                │
   Build & Test     Build & Lint     Build & Push
   Backend          Frontend         Both (on tag)
        │                │                │
        ▼                ▼                ▼
   PR Comments      PR Status       Docker Hub
   Test Reports     Coverage        Registry
```

---

## Recommended UI Publishing Enhancement

### Proposed: **Extend publish-docker.yml to include UI**

Currently, `publish-docker.yml` only builds the .NET API. To **also publish the UI** as a separate Docker image:

#### Option A: Extend Current Workflow

Modify `publish-docker.yml` to build **both** images:

```yaml
name: Publish Docker Images

on:
  push:
    tags:
      - 'v*'
  workflow_dispatch:
    inputs:
      version:
        description: 'Version to publish (e.g., 1.2.3)'
        required: false

env:
  DOCKERHUB_REPO: sebfischer83/arquivomate2

jobs:
  # Existing backend job
  publish-api:
    runs-on: ubuntu-latest
    steps:
      # ... existing API build steps ...

  # New UI job
  publish-ui:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Set VERSION
        run: |
          if [[ "${{ github.ref }}" == refs/tags/* ]]; then
            echo "VERSION=${GITHUB_REF#refs/tags/}" >> $GITHUB_ENV
          elif [ -n "${{ github.event.inputs.version }}" ]; then
            echo "VERSION=${{ github.event.inputs.version }}" >> $GITHUB_ENV
          else
            echo "VERSION=0.0.0-local" >> $GITHUB_ENV
          fi

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2

      - name: Login to Docker Hub
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}

      - name: Build and push UI image
        uses: docker/build-push-action@v4
        with:
          context: src/ArquivoMate2.Ui
          file: src/ArquivoMate2.Ui/Dockerfile
          push: true
          platforms: linux/amd64,linux/arm64
          tags: |
            ${{ env.DOCKERHUB_REPO }}-ui:${{ env.VERSION }}
            ${{ env.DOCKERHUB_REPO }}-ui:latest
          build-args: |
            VERSION=${{ env.VERSION }}
```

**Result:**
- `sebfischer83/arquivomate2-api:v1.0.0` (Backend)
- `sebfischer83/arquivomate2-ui:v1.0.0` (Frontend)

---

#### Option B: Separate UI-only Publishing Workflow

Create new file: `.github/workflows/publish-ui-docker.yml`

```yaml
name: Publish UI Docker Image

on:
  push:
    tags:
      - 'ui-v*'  # Tag format: ui-v1.0.0
    paths:
      - 'src/ArquivoMate2.Ui/**'
  workflow_dispatch:
    inputs:
      version:
        description: 'UI version to publish'
        required: false

jobs:
  publish-ui:
    runs-on: ubuntu-latest
    steps:
      - name: Checkout
        uses: actions/checkout@v4

      - name: Parse version
        run: |
          VERSION="${GITHUB_REF#refs/tags/ui-}"
          if [ "$VERSION" = "$GITHUB_REF" ]; then
            VERSION="${{ github.event.inputs.version }}"
          fi
          echo "VERSION=${VERSION:-0.0.0-local}" >> $GITHUB_ENV

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v2

      - name: Login to Docker Hub
        uses: docker/login-action@v2
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}

      - name: Build and push
        uses: docker/build-push-action@v4
        with:
          context: src/ArquivoMate2.Ui
          file: src/ArquivoMate2.Ui/Dockerfile
          push: true
          platforms: linux/amd64,linux/arm64
          tags: |
            sebfischer83/arquivomate2-ui:${{ env.VERSION }}
            sebfischer83/arquivomate2-ui:latest
```

**Usage:**
```bash
git tag ui-v1.0.0
git push origin ui-v1.0.0
```

---

## Current Workflow File Locations

```
.github/
├── workflows/
│   ├── ci.yml                 # Backend (.NET) CI
│   ├── ui-ci.yml              # Frontend (Angular) CI
│   └── publish-docker.yml     # Docker publishing (currently backend only)
```

---

## Recommended Actions

### Phase 1: Enhance UI CI (Immediate)
Update `ui-ci.yml` to also:
- ✅ Run unit tests: `npm test`
- ✅ Collect coverage reports
- ✅ Post coverage to PRs
- ✅ Build Docker image (for testing, don't push)

```yaml
# Add to ui-ci.yml
- name: Run tests with coverage
  working-directory: src/ArquivoMate2.Ui
  run: npm test -- --code-coverage --watch=false

- name: Build Docker image
  working-directory: src/ArquivoMate2.Ui
  run: docker build -t arquivomate2-ui:test .
```

### Phase 2: Publish UI Docker (Short-term)
Add UI publishing to `publish-docker.yml` or create `publish-ui-docker.yml`:
- Build multi-arch UI image
- Push to Docker Hub
- Use semantic versioning: `ui-v1.0.0` or just include in main `v1.0.0` tag

### Phase 3: CD/Deployment (Medium-term)
Add deployment workflows:
- Deploy to staging on PR merge
- Deploy to production on release tag
- Update Kubernetes manifests
- Health checks post-deployment

---

## Secret Management

Both workflows require Docker Hub credentials. Check `.github/` settings:

**Required Secrets:**
```
DOCKER_USERNAME    # Docker Hub username
DOCKER_PASSWORD    # Docker Hub password (or personal access token)
```

**To set (if not already done):**
1. Go to GitHub repo → Settings → Secrets and variables → Actions
2. Add:
   - `DOCKER_USERNAME` = `sebfischer83`
   - `DOCKER_PASSWORD` = (Docker Hub token)

---

## Deployment Strategy

### Recommended versioning:

```
Main Release Tag (Backend + UI):
  git tag v1.0.0
  └─ Publishes both:
     - sebfischer83/arquivomate2:v1.0.0 (API)
     - sebfischer83/arquivomate2-ui:v1.0.0 (UI)

Separate UI Release:
  git tag ui-v1.0.0
  └─ Publishes only:
     - sebfischer83/arquivomate2-ui:ui-v1.0.0

Latest Tags:
  sebfischer83/arquivomate2:latest        (API latest stable)
  sebfischer83/arquivomate2-ui:latest     (UI latest stable)
```

---

## Quick Reference: Workflow Triggers

| Workflow | Trigger | What Builds |
|----------|---------|------------|
| `ci.yml` | Push to `master` (backend changes) | .NET API |
| `ui-ci.yml` | Push to `src/ArquivoMate2.Ui/**` | Angular UI |
| `publish-docker.yml` | Tag `v*` or manual trigger | .NET API (+ optional UI) |

---

## Next Steps

1. **Verify secrets are configured** in GitHub Actions settings
2. **Test publish-docker.yml** with a test tag: `git tag v0.0.1-test && git push --tags`
3. **Enhance ui-ci.yml** to run tests and generate coverage
4. **Decide on UI publishing strategy** (Option A or B above)
5. **Document versioning** for team (when to use `v*` vs `ui-v*` tags)

---

## References

- Existing workflows: `.github/workflows/*.yml`
- Docker configuration: `src/ArquivoMate2.Ui/Dockerfile`
- Runtime config: `src/ArquivoMate2.Ui/docs/RUNTIME_CONFIG.md`
- Build guide: `src/ArquivoMate2.Ui/docs/DOCKER_BUILD_GUIDE.md`
