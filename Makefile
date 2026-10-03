up:
	docker compose up -d --build
stop:
	docker compose down
logs:
	docker compose logs -f app
check:
	docker compose --profile tools run --rm check
smoke:
	docker compose --profile tools run --rm smoke
browser:
	docker compose --profile tools run --rm browser
security:
	node --test scripts/media-preflight.test.mjs
	node scripts/security-smoke.mjs $(PROJECT)
refresh:
	docker compose up -d --build app

offline-check:
	node --test scripts/offline.test.mjs
	OFFLINE_TEST_PROJECT=$(PROJECT) docker compose -p $(PROJECT) --profile tools run --rm offline-browser

auth-check:
	AUTH_TEST_PROJECT=$(PROJECT) docker compose -p $(PROJECT) --profile tools run --rm auth-browser

observability:
	TELEMETRY_ENABLED=true docker compose --profile observability up -d --build app otel-collector

observability-logs:
	docker compose --profile observability logs -f otel-collector

ci-config:
	node scripts/validate-production.mjs
	docker run --rm --network none -v "$(CURDIR):/work:ro" -w /work mcr.microsoft.com/playwright:v1.63.0-noble node --test scripts/deployment.test.mjs

.PHONY: secrets-check
# Scan every reachable commit before pushing; output always redacts findings.
secrets-check:
	docker run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges:true -v "$(CURDIR):/repo:ro" ghcr.io/gitleaks/gitleaks:v8.30.1@sha256:c00b6bd0aeb3071cbcb79009cb16a60dd9e0a7c60e2be9ab65d25e6bc8abbb7f git --config /repo/.gitleaks.toml --redact=100 --no-banner --log-opts=--all /repo
