# Production on the shared cyrino droplet

## Trigger and release flow

- `.github/workflows/ci.yml` runs on **push to `main` only**. Direct pushes and
  completed merges trigger it. Opening/updating a pull request, pushing another
  branch and creating a tag do not trigger this workflow.
- The only GitHub deployment environment is `production`. There is no staging
  release or manual workflow trigger. GitHub serializes runs; newer pushes may
  replace a pending run while an active deployment finishes.
- `verify`: locked .NET build/tests, JavaScript policy/deployment checks and real
  PostgreSQL/Garage/ClamAV integration, security and browser suites on a disposable
  CI stack. Builds one release image and exports that exact tested image.
- `publish`: loads the tested artifact and pushes the commit tag to GHCR without
  rebuilding. Its output is the immutable `ghcr.io/<owner>/<repo>@sha256:...`
  reference. The OCI source label links the package to its GitHub repository.
- `deploy`: copies only production configuration/provisioning files via SSH,
  reads the droplet-owned `.env` without changing it, and pulls
  the tested digest. It takes a PostgreSQL backup, provisions private Garage
  permissions, applies committed EF migrations, starts the app and checks HTTPS
  readiness through the shared Caddy. Successful releases update `current`.
- Test containers stay on GitHub runners. Production has four continuous
  services (`app`, `postgres`, `garage`, `clamav`) and two deployment jobs
  (`garage-init`, `migrate`), which exit and are removed after running.


## Disposable CI scanner startup

- The verification job uses `docker-compose.yml` plus `docker-compose.ci.yml`.
  Only CI switches to the same ClamAV engine image with bundled signatures and
  stores public signature files under ignored `output/clamav`.
- GitHub Actions restores `.cvd`, `.cld` and detached `.sign` files from prior
  successful main runs. It never caches `freshclam.dat`, credentials, application
  media or database volumes. Missing databases are seeded from the official image;
  an existing newer `.cld` is not replaced with the bundled `.cvd`.
- FreshClam updates/verifies databases before starting the daemon, avoiding a lost
  reload notification during initialization. Bundled files may be old: CI waits for
  a running daemon with definitions no older than 72 hours before starting the app.
  Production uses its existing `_base` image and persistent signature volume;
  application freshness checks and fail-closed upload scanning stay enabled.
- An empty cache can still require an initial update. CDN errors, stale definitions
  or memory exhaustion fail verification instead of allowing unscanned uploads.
  The workflow prints scanner logs, recent health results and OOM/exit state before
  deleting disposable CI containers. It never prints resolved runtime environments.
- To investigate a failure, open **Diagnose failed CI stack** in Actions. `403`/`429`
  FreshClam responses indicate CDN blocking/rate limits; `OOMKilled` indicates a
  memory kill; restart counts and health output report repeated exits, daemon
  availability and signature age.
  Increasing the timeout does not fix a blocked CDN or memory exhaustion.
- The concurrency smoke check accepts a verified `429`/`Retry-After: 1` on a burst
  request or its probe. Canceled uploads may still hold permits when the next burst
  arrives, so checking only a later probe can miss an earlier correct rejection.

## SSH disconnects and production startup

- SSH/SCP use encrypted keepalive requests every 30 seconds and tolerate six
  unanswered requests. Host-key verification remains required. Keepalives help
  with idle connections; they cannot prevent a droplet reboot or memory kill.
- Before registry login, image pulls or service changes, Linux `MemTotal` must
  report at least 3 GiB of physical RAM. This rejects undersized 1–2 GiB hosts;
  swap is not counted. This floor does not reserve RAM or guarantee enough headroom
  on a busy shared host. A 4 GiB plan usually passes after kernel reservations,
  but the scanner, application, database and existing services still share that RAM.
- Startup reports each release stage and container state, health, OOM flag,
  exit code and restart count. Waiting services report progress every 30 seconds.
  Missing, stopped, unhealthy or OOM-killed dependencies stop the rollout before
  backup, migration or app replacement. The startup deadline remains 20 minutes.
- Each attempted release stores only these selected diagnostics and host memory/
  disk snapshots in `<DEPLOY_PATH>/releases/<release-id>/deployment.log`, mode 0600.
  No runtime dotenv, resolved Compose environment or raw service logs are copied
  into it or uploaded to GitHub. The droplet's `.env` stays unchanged.
- A lost SSH connection fails Actions with exit 255 and identifies the status-log
  path. Completion is unknown: inspect the server before retrying. There is no
  automatic replay of provisioning or migrations after a transport failure.
  Handled hangup/termination signals record failure and preserve/restore the prior
  app; a host crash or SIGKILL can prevent traps and cleanup from running.
- The Docker login warning is expected with a temporary mode-0700 registry
  directory. It is removed when the remote script exits normally or handles an
  error. It is separate from `client_loop: ... Broken pipe`.
- ClamAV's official guidance recommends at least 3 GiB, preferably 4 GiB **for
  the scanner**, with additional capacity for PostgreSQL, Garage, the app and
  existing droplet services. Its `mem_limit: 4g` is a ceiling; it does not create
  host RAM. Do not bypass scanning or delete persistent volumes to retry.

For a failed first deployment (including attempts made before status logs existed),
run these read-only checks through your trusted droplet SSH session:

```sh
free -h
df -h /opt/i-have-been
sudo journalctl -k --since '1 hour ago' --no-pager | grep -Ei 'out of memory|oom|killed process'
docker inspect --format 'state={{.State.Status}} oom={{.State.OOMKilled}} exit={{.State.ExitCode}} restarts={{.RestartCount}} health={{if .State.Health}}{{.State.Health.Status}}{{end}}' i-have-been-clamav-1
```

Kernel OOM entries confirm memory kills; the scanner flag identifies a container
kill if Docker still retains that state. An empty kernel search alone does not
rule out a reboot, older incident or lost logs. If the scanner remains unavailable,
review `docker logs --tail 100 i-have-been-clamav-1` locally for definition-update
or daemon errors. Avoid sharing `.env`, full `docker inspect` or resolved
`docker compose config` output. Once you have resolved the cause, push the updated
scripts to `main`; rerunning an older workflow does not use newer code.

## Confirmed memory exhaustion on the current droplet

On 2026-10-06, the operator reported 961 MiB total RAM, 259 MiB available and
no swap. The kernel recorded `global_oom` and killed `clamd`. PostgreSQL invoked
the OOM handler during an allocation; the victim was the scanner. This confirms
host memory exhaustion during scanner startup. SSH keepalives cannot remedy it.

1. Stop the failing scanner while addressing capacity:
   `docker stop i-have-been-clamav-1`. This preserves its definitions and all
   database/media volumes. Uploads must continue to require a healthy scanner.
2. Increase capacity before retrying. A 4 GiB plan is a constrained starting point
   for a lightly loaded installation; for this shared droplet, 8 GiB is a planning
   recommendation, subject to measuring existing service usage. Another option is
   moving the scanner to a private 4 GiB host, which requires private-network and
   application configuration work before deployment. Do not expose port 3310.
3. For an in-place DigitalOcean resize, back up the shared host, schedule downtime
   for every hosted application, shut down cleanly and select **CPU and RAM only**
   in the droplet's **Resize** page. Disk expansion is permanent; a memory-only
   resize preserves the possibility of downsizing later. Power on afterward.
4. Verify RAM with `free -h` and inspect other services with `docker ps` before
   pushing the updated scripts to `main` and retrying deployment. Review memory
   usage during scanner startup and signature updates; passing preflight alone
   does not prove that the shared host has sufficient spare capacity.

Swap can cushion temporary pressure but is not an adequate production plan for
this complete stack on a 1 GiB host. No droplet resize, scanner stop, volume change
or runtime-secret modification was performed by the local code change.

## Primary references

- [DigitalOcean resize procedure](https://docs.digitalocean.com/products/droplets/how-to/resize/)
- [OpenSSH keepalive options](https://man.openbsd.org/ssh_config.5#ServerAliveInterval)
- [ClamAV Docker memory requirements](https://docs.clamav.net/manual/Installing/Docker.html#memory-ram-requirements)
- [Docker registry credential storage](https://docs.docker.com/reference/cli/docker/login/#credential-stores)
- [GitHub push event filters](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#onpushpull_requestpull_request_targetpathspaths-ignore)
- [Publishing images to GHCR](https://docs.github.com/en/actions/tutorials/publish-packages/publish-docker-images)
- [Container registry access and digests](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-container-registry)
- [GitHub deployment environments](https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments)
- [caddy-docker-proxy routing labels](https://github.com/lucaslorentz/caddy-docker-proxy)
