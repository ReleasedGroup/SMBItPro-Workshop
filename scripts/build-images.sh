#!/usr/bin/env bash
set -euo pipefail

TAG="${1:-local}"

docker build -f docker/Dockerfile.api -t helpdesk-light-api:${TAG} .
docker build -f docker/Dockerfile.worker -t helpdesk-light-worker:${TAG} .
docker build -f docker/Dockerfile.web -t helpdesk-light-web:${TAG} .

echo "Built images with tag: ${TAG}"
