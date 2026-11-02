# CI/CD and integration testing

## Pipeline

Pull requests to master and master pushes run the existing .NET build/tests and
Python QA tests/evaluation plus black-box process integration tests. The image job
waits for all three gates, builds the non-root Document QA image and runs the same
HTTP lifecycle against a container with a named volume. A tested image is saved as
a short-lived Actions artifact. Only a successful master push publishes that exact
artifact to `ghcr.io/<owner>/<repository>/document-qa:<commit-sha>` using the workflow's
GITHUB_TOKEN. PRs, including forks, never receive package-write credentials or publish.
Package creation must be permitted by repository/organization settings.

This is continuous delivery to GHCR. No production host has been configured and no
automatic production rollout is performed. The existing Kafka/MySQL CDC stack is
not covered by the new black-box tests; its .NET test job remains in place.

## Reproduce locally

```bash
python -m unittest discover -s document_qa/tests -v
python -m unittest discover -s document_qa/integration_tests -v
python -m document_qa.evaluate
docker build -f document_qa/Dockerfile -t document-qa:test .
QA_TEST_IMAGE=document-qa:test python -m unittest discover -s document_qa/integration_tests -v
```

The process tests spawn the real module with a temporary disk database and random
credentials. They verify missing authentication, required confirmation, replay
rejection, citations, persistence across restart, loss of pending approval after
restart, deletion/abstention and durable audit records. A second test checks startup
without identity configuration fails. Container mode repeats the lifecycle through
published HTTP ports and a persistent Docker volume; it skips the process-only
startup check and direct audit inspection. Containers, volumes and processes are
cleaned up, including on assertion failure. Tests use bounded readiness polling.

## Deploy a published image

On a Docker host, authenticate to GHCR if the package is private. Obtain the digest
from the successful publish logs/registry, then configure:

```bash
export QA_IMAGE='ghcr.io/OWNER/cdc/document-qa@sha256:REPLACE_WITH_PUBLISHED_DIGEST'
# Set DOCUMENT_QA_IDENTITIES to trusted identity JSON as documented in document-qa.md.
docker compose -f deploy/document-qa.compose.yml config --quiet
docker compose -f deploy/document-qa.compose.yml pull
docker compose -f deploy/document-qa.compose.yml up -d --wait --wait-timeout 60
curl --fail http://127.0.0.1:8090/health
```

Preserve the named volume when upgrading. Back up SQLite using its online backup
API before an upgrade; keep the previous image digest. To roll back, set QA_IMAGE
to that digest and repeat pull/up. Do not use `down -v` unless intentionally deleting
all QA documents and audit history. Application/schema compatibility must be
checked before rolling back a future migration.

Compose exposes only host loopback, drops capabilities, uses a read-only root
filesystem and a writable data volume. The image explicitly binds 0.0.0.0 inside
the container; direct Python execution retains its loopback default. Secrets are
runtime environment values, never baked into the image; protect host configuration
and do not paste resolved Compose config into logs. Use a managed secret mechanism
and a TLS reverse proxy before remote access. One service instance is supported;
RAM confirmations/rate limits are not shared across replicas.

Health checks indicate process HTTP availability, not corpus quality. Container
build and smoke checks must pass on Actions before treating the image as verified.
Base image tags are mutable; lock to reviewed base digests for reproducible releases.
