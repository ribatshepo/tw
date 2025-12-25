.PHONY: help
.DEFAULT_GOAL := help

# ================================================================================================
# GBMM Platform - Makefile
# ================================================================================================
# Build, test, and deploy automation for the GBMM platform
# ================================================================================================

# Colors for output
RESET := \033[0m
BOLD := \033[1m
GREEN := \033[32m
YELLOW := \033[33m
BLUE := \033[34m
CYAN := \033[36m

# Project configuration
PROJECT_NAME := gbmm-platform
DOCKER_COMPOSE_INFRA := docker-compose.infra.yml
DOCKER_COMPOSE_SERVICES := docker-compose.yml

# ================================================================================================
# HELP
# ================================================================================================

help: ## Show this help message
	@echo ""
	@echo "$(BOLD)$(CYAN)GBMM Platform - Makefile Commands$(RESET)"
	@echo ""
	@echo "$(BOLD)Infrastructure:$(RESET)"
	@grep -E '^infra-[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(CYAN)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Setup & Configuration:$(RESET)"
	@grep -E '^setup[a-zA-Z0-9_-]*:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Development:$(RESET)"
	@grep -E '^dev[a-zA-Z0-9_-]*:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(YELLOW)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Services:$(RESET)"
	@grep -E '^services-[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(BLUE)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Build:$(RESET)"
	@grep -E '^build-[a-zA-Z0-9_-]+:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(CYAN)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Testing:$(RESET)"
	@grep -E '^test[a-zA-Z0-9_-]*:.*?## .*$$|^smoke-test:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(GREEN)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Cleanup:$(RESET)"
	@grep -E '^clean[a-zA-Z0-9_-]*:.*?## .*$$' $(MAKEFILE_LIST) | sort | awk 'BEGIN {FS = ":.*?## "}; {printf "  $(YELLOW)%-25s$(RESET) %s\n", $$1, $$2}'
	@echo ""
	@echo "$(BOLD)Examples:$(RESET)"
	@echo "  $(CYAN)make dev$(RESET)              - Start infrastructure and run setup (first time)"
	@echo "  $(CYAN)make infra-up$(RESET)         - Start infrastructure services"
	@echo "  $(CYAN)make smoke-test$(RESET)       - Run smoke tests to validate setup"
	@echo "  $(CYAN)make clean$(RESET)            - Clean up everything"
	@echo ""

# ================================================================================================
# INFRASTRUCTURE TARGETS
# ================================================================================================

infra-up: ## Start all infrastructure services
	@echo "$(BOLD)$(CYAN)Starting infrastructure services...$(RESET)"
	docker-compose -f $(DOCKER_COMPOSE_INFRA) up -d
	@echo "$(GREEN)✓ Infrastructure services started$(RESET)"
	@echo ""
	@echo "$(YELLOW)Waiting for services to become healthy...$(RESET)"
	@./scripts/wait-for-infrastructure.sh

infra-down: ## Stop all infrastructure services
	@echo "$(BOLD)$(CYAN)Stopping infrastructure services...$(RESET)"
	docker-compose -f $(DOCKER_COMPOSE_INFRA) down
	@echo "$(GREEN)✓ Infrastructure services stopped$(RESET)"

infra-restart: infra-down infra-up ## Restart all infrastructure services

infra-logs: ## Show logs from infrastructure services
	docker-compose -f $(DOCKER_COMPOSE_INFRA) logs -f

infra-ps: ## Show status of infrastructure services
	@echo "$(BOLD)$(CYAN)Infrastructure Services Status:$(RESET)"
	@docker-compose -f $(DOCKER_COMPOSE_INFRA) ps

# ================================================================================================
# SETUP TARGETS
# ================================================================================================

setup: setup-env setup-certs setup-databases ## Run complete setup (first time)
	@echo ""
	@echo "$(BOLD)$(GREEN)✓ Setup complete!$(RESET)"
	@echo ""
	@echo "$(YELLOW)Next steps:$(RESET)"
	@echo "  1. Review the generated .env file and update passwords"
	@echo "  2. Run 'make smoke-test' to validate the setup"
	@echo "  3. Phase 0 complete - infrastructure ready!"
	@echo "  4. When services are implemented, run 'make services-up'"
	@echo ""

setup-env: ## Create .env file from template
	@if [ ! -f .env ]; then \
		echo "$(CYAN)Creating .env file from template...$(RESET)"; \
		cp .env.template .env; \
		echo "$(GREEN)✓ .env file created$(RESET)"; \
		echo "$(YELLOW)⚠  Please update passwords in .env file!$(RESET)"; \
	else \
		echo "$(YELLOW).env file already exists, skipping...$(RESET)"; \
	fi

setup-certs: ## Generate development mTLS certificates
	@echo "$(BOLD)$(CYAN)Generating mTLS certificates...$(RESET)"
	@./scripts/generate-dev-certs.sh
	@echo "$(GREEN)✓ Certificates generated$(RESET)"

# Note: setup-vault removed - USP (Unified Security Platform) provides secrets management
# USP will be added as a service in docker-compose.yml when services are implemented
# For Phase 0 (infrastructure only), secrets are in .env file
# For Phase 1 (service development), use: make setup-dev-secrets (to be implemented)

setup-databases: infra-up ## Initialize databases and schemas
	@echo "$(BOLD)$(CYAN)Initializing databases...$(RESET)"
	@./scripts/init-databases.sh
	@echo "$(GREEN)✓ Databases initialized$(RESET)"

# ================================================================================================
# DEVELOPMENT WORKFLOW
# ================================================================================================

dev: infra-up setup ## Start infrastructure and run setup (recommended for first time)
	@echo ""
	@echo "$(BOLD)$(GREEN)✓ Development environment ready!$(RESET)"
	@echo ""

dev-reset: clean dev ## Clean and restart development environment

# ================================================================================================
# SERVICE TARGETS (Stubs - will be implemented when services are built)
# ================================================================================================

services-up: ## Start all application services
	@echo "$(YELLOW)⚠  Services not yet implemented$(RESET)"
	@echo "$(CYAN)When services are built, this will start them with:$(RESET)"
	@echo "  docker-compose -f $(DOCKER_COMPOSE_SERVICES) up -d"

services-down: ## Stop all application services
	@echo "$(YELLOW)⚠  Services not yet implemented$(RESET)"
	@echo "$(CYAN)When services are built, this will stop them with:$(RESET)"
	@echo "  docker-compose -f $(DOCKER_COMPOSE_SERVICES) down"

services-restart: services-down services-up ## Restart all application services

services-logs: ## Show logs from application services
	@echo "$(YELLOW)⚠  Services not yet implemented$(RESET)"
	@echo "$(CYAN)When services are built, this will show logs with:$(RESET)"
	@echo "  docker-compose -f $(DOCKER_COMPOSE_SERVICES) logs -f"

services-ps: ## Show status of application services
	@echo "$(YELLOW)⚠  Services not yet implemented$(RESET)"
	@echo "$(CYAN)When services are built, this will show status with:$(RESET)"
	@echo "  docker-compose -f $(DOCKER_COMPOSE_SERVICES) ps"

# ================================================================================================
# BUILD TARGETS (Stubs - will be implemented when services are built)
# ================================================================================================

build-all: build-uccp build-nccs build-usp build-udps build-stream ## Build all services

build-uccp: ## Build UCCP service
	@echo "$(YELLOW)⚠  UCCP not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will build with: cd services/uccp && go build$(RESET)"

build-nccs: ## Build NCCS service
	@echo "$(YELLOW)⚠  NCCS not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will build with: cd services/nccs && dotnet build$(RESET)"

build-usp: ## Build USP service
	@echo "$(YELLOW)⚠  USP not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will build with: cd services/usp && dotnet build$(RESET)"

build-udps: ## Build UDPS service
	@echo "$(YELLOW)⚠  UDPS not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will build with: cd services/udps && sbt compile$(RESET)"

build-stream: ## Build Stream Compute service
	@echo "$(YELLOW)⚠  Stream Compute not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will build with: cd services/stream-compute && cargo build$(RESET)"

# ================================================================================================
# TESTING TARGETS
# ================================================================================================

test: test-unit test-integration ## Run all tests

test-unit: ## Run unit tests for all services
	@echo "$(YELLOW)⚠  Tests not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will run unit tests for all services$(RESET)"

test-integration: ## Run integration tests
	@echo "$(YELLOW)⚠  Integration tests not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will run: pytest tests/integration$(RESET)"

test-e2e: ## Run end-to-end tests
	@echo "$(YELLOW)⚠  E2E tests not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will run: pytest tests/e2e$(RESET)"

test-load: ## Run load tests
	@echo "$(YELLOW)⚠  Load tests not yet implemented$(RESET)"
	@echo "$(CYAN)When implemented, this will run load tests$(RESET)"

smoke-test: ## Run smoke tests to validate infrastructure setup
	@echo "$(BOLD)$(CYAN)Running smoke tests...$(RESET)"
	@./scripts/smoke-tests.sh

# ================================================================================================
# CLEANUP TARGETS
# ================================================================================================

clean: clean-containers clean-volumes ## Clean up containers and volumes (prompts for confirmation)

clean-containers: ## Stop and remove all containers
	@echo "$(BOLD)$(YELLOW)Stopping and removing containers...$(RESET)"
	docker-compose -f $(DOCKER_COMPOSE_INFRA) down
	@if [ -f "$(DOCKER_COMPOSE_SERVICES)" ]; then \
		docker-compose -f $(DOCKER_COMPOSE_SERVICES) down; \
	fi
	@echo "$(GREEN)✓ Containers removed$(RESET)"

clean-volumes: ## Remove all volumes (prompts for confirmation)
	@echo "$(BOLD)$(YELLOW)⚠  WARNING: This will delete all data in volumes!$(RESET)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		docker-compose -f $(DOCKER_COMPOSE_INFRA) down -v; \
		echo "$(GREEN)✓ Volumes removed$(RESET)"; \
	else \
		echo "$(CYAN)Cancelled$(RESET)"; \
	fi

clean-certs: ## Remove generated certificates
	@echo "$(CYAN)Removing certificates...$(RESET)"
	@./scripts/cleanup.sh --certs-only
	@echo "$(GREEN)✓ Certificates removed$(RESET)"

clean-secrets: ## Remove generated secrets
	@echo "$(CYAN)Removing secrets...$(RESET)"
	@./scripts/cleanup.sh --secrets-only
	@echo "$(GREEN)✓ Secrets removed$(RESET)"

clean-all: clean ## Complete cleanup (alias for clean)

# ================================================================================================
# UTILITY TARGETS
# ================================================================================================

status: infra-ps services-ps ## Show status of all services

logs: ## Show logs from all services (requires service name: make logs SERVICE=postgres)
	@if [ -z "$(SERVICE)" ]; then \
		echo "$(YELLOW)Usage: make logs SERVICE=<service-name>$(RESET)"; \
		echo "$(CYAN)Available services: postgres, redis, kafka, vault, etc.$(RESET)"; \
	else \
		docker logs -f gbmm-$(SERVICE); \
	fi

shell: ## Open shell in a container (requires service name: make shell SERVICE=postgres)
	@if [ -z "$(SERVICE)" ]; then \
		echo "$(YELLOW)Usage: make shell SERVICE=<service-name>$(RESET)"; \
		echo "$(CYAN)Available services: postgres, redis, kafka, vault, etc.$(RESET)"; \
	else \
		docker exec -it gbmm-$(SERVICE) /bin/bash || docker exec -it gbmm-$(SERVICE) /bin/sh; \
	fi

validate-env: ## Validate .env file has required variables
	@./scripts/helpers/validation.sh

# ================================================================================================
# DEPLOYMENT TARGETS (Kubernetes/Helm)
# ================================================================================================

deploy-infra-dev: ## Deploy infrastructure to Kubernetes (dev)
	@echo "$(CYAN)Deploying infrastructure to Kubernetes (dev)...$(RESET)"
	helm install gbmm-infrastructure ./deploy/helm/infrastructure \
		--namespace gbmm-infra \
		--create-namespace \
		--values ./deploy/helm/infrastructure/values-dev.yaml
	@echo "$(GREEN)✓ Infrastructure deployed$(RESET)"

deploy-infra-staging: ## Deploy infrastructure to Kubernetes (staging)
	@echo "$(CYAN)Deploying infrastructure to Kubernetes (staging)...$(RESET)"
	helm install gbmm-infrastructure ./deploy/helm/infrastructure \
		--namespace gbmm-infra \
		--create-namespace \
		--values ./deploy/helm/infrastructure/values-staging.yaml
	@echo "$(GREEN)✓ Infrastructure deployed$(RESET)"

deploy-infra-prod: ## Deploy infrastructure to Kubernetes (production)
	@echo "$(BOLD)$(YELLOW)⚠  WARNING: Deploying to PRODUCTION!$(RESET)"
	@read -p "Are you sure? [y/N] " -n 1 -r; \
	echo; \
	if [[ $$REPLY =~ ^[Yy]$$ ]]; then \
		helm install gbmm-infrastructure ./deploy/helm/infrastructure \
			--namespace gbmm-infra \
			--create-namespace \
			--values ./deploy/helm/infrastructure/values-prod.yaml; \
		echo "$(GREEN)✓ Infrastructure deployed$(RESET)"; \
	else \
		echo "$(CYAN)Cancelled$(RESET)"; \
	fi

# ================================================================================================
# INFORMATION
# ================================================================================================

info: ## Show environment information
	@echo "$(BOLD)$(CYAN)GBMM Platform Information$(RESET)"
	@echo ""
	@echo "$(BOLD)Project:$(RESET) $(PROJECT_NAME)"
	@echo "$(BOLD)Docker Compose (Infra):$(RESET) $(DOCKER_COMPOSE_INFRA)"
	@echo "$(BOLD)Docker Compose (Services):$(RESET) $(DOCKER_COMPOSE_SERVICES)"
	@echo ""
	@echo "$(BOLD)Services:$(RESET)"
	@echo "  - UCCP (Unified Compute & Coordination Platform)"
	@echo "  - NCCS (.NET Compute Client Service)"
	@echo "  - USP (Unified Security Platform)"
	@echo "  - UDPS (Unified Data Platform Service)"
	@echo "  - Stream Compute Service"
	@echo ""
	@echo "$(BOLD)Infrastructure:$(RESET)"
	@echo "  - PostgreSQL (Database)"
	@echo "  - Redis (Cache)"
	@echo "  - Kafka (Event Streaming)"
	@echo "  - MinIO (Object Storage)"
	@echo "  - Vault (Secrets Management)"
	@echo "  - RabbitMQ (Message Broker)"
	@echo "  - Prometheus (Metrics)"
	@echo "  - Grafana (Visualization)"
	@echo "  - Jaeger (Tracing)"
	@echo "  - Elasticsearch (Logs)"
	@echo ""
