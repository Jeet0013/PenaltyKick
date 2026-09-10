# Penalty Kick

A 3D penalty shootout that runs in a browser, with no server and no install.

## Running it

```bash
npm install
npm run dev
```

The dev server is exposed on the local network, so the address it prints can be
opened on a phone on the same wifi — which is where this should be tested.

## Building

```bash
npm run build         # typecheck, tests, production build → dist/
npm run build:single  # one self-contained HTML file → dist-single/
```

The single-file build is what gets published: one `index.html` with everything
inlined, so it works from any static host, or opened straight off disk.

## Deploying

Merging to `main` builds and publishes automatically, gated on typecheck and
tests. Nothing is deployed by hand.
