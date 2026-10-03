# I Have Been

A private travel journal with a draggable world map, clear country polygons, detailed memories and original photo/video attachments. C# frontend (Blazor Interactive Server) and ASP.NET Core backend ship as **one .NET 10 monolith**, backed by PostgreSQL and private Garage object storage.

this project is deployed under https://been.joaocyrino.com

## Run locally

Requirements: Docker with Compose, Python 3, and sufficient Docker memory for
ClamAV (3–4 GiB for the scanner in addition to the app, PostgreSQL and Garage).
No host .NET SDK or Node installation is needed to run the app. The first scanner
startup downloads virus definitions and can take several minutes.

```sh
python3 scripts/init-env.py
docker compose up -d --build
```

Open **http://localhost:4188**, create an account, select a country, zoom in to level 8 or closer, then click again at the place you want to pin. You can also use “Add a memory”. Upload photos/videos from the memory details. No sample account or personal travel/media data is included. Startup runs versioned PostgreSQL migrations and idempotently provisions a private Garage bucket.

```sh
make logs
make check
make smoke
make stop
```

`make stop` preserves all named volumes. Never remove volumes to restart the project. The app port is loopback-only. PostgreSQL, Garage S3, Garage RPC and Garage admin have **no published ports**.

## Structure

```text
src/
  IHaveBeen.Domain/          encapsulated entities and business invariants
  IHaveBeen.Application/     feature use cases, DTOs, results and adapter interfaces
  IHaveBeen.Infrastructure/  PostgreSQL/Identity/Garage adapters and migrations
  IHaveBeen.Web/
    Components/             focused Blazor components and page coordination
    Features/               thin authenticated HTTP endpoints and contracts
    Configuration/          service composition and request pipeline
    wwwroot/                CSS, Leaflet interop and public map assets only
tests/                      domain/use-case tests and architecture enforcement
infra/garage/               Garage configuration (private, single-node)
infra/clamav/               private scanner, scan limits and signature updates
scripts/                    setup, provisioning and real-stack verification
docs/                       schema and security/deployment decisions
```

The small JavaScript bridge handles Leaflet, browser fetch/CSRF, file transport and clipboard APIs; the owner UI/state is in C# Blazor components. Browser HTTP requests always pass through API authentication, even when made from an already connected Blazor circuit. The shared viewer is deliberately a read-only page with no server-side owner circuit.
