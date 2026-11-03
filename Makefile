.PHONY: dev dev-fresh seed-dev db-scenarios check-backend-port kill-backend infra infra-down infra-reset ensure-ef migrate backend frontend install generate-api test test-backend test-frontend skills skills-validate

DEV_ENV = Auth__CookieDomain=.grundet.test Billing__CreateTeamAlphaWorkspaces=true
DEV_EMAIL = alex@grundet.test
DEV_NAME = Alex Morgan
DEV_SECOND_EMAIL = sam@grundet.test
DEV_SECOND_NAME = Sam Rivera
SCENARIOS ?=

dev: check-backend-port infra migrate seed-dev db-scenarios
	@$(MAKE) --no-print-directory -j2 _watch-backend _watch-frontend

dev-fresh: check-backend-port infra-reset migrate seed-dev db-scenarios
	@$(MAKE) --no-print-directory -j2 _watch-backend _watch-frontend

check-backend-port:
	@pid=$$(lsof -tiTCP:8080 -sTCP:LISTEN 2>/dev/null); \
	if [ -n "$$pid" ]; then \
		echo "Port 8080 is already in use by PID(s): $$pid"; \
		echo "Run 'make kill-backend' if this is a stale Grundet.Api/dotnet watch process."; \
		exit 1; \
	fi

kill-backend:
	@pid=$$(lsof -tiTCP:8080 -sTCP:LISTEN 2>/dev/null); \
	if [ -z "$$pid" ]; then \
		echo "No process is listening on port 8080."; \
	else \
		echo "Killing process(es) listening on port 8080: $$pid"; \
		kill $$pid; \
	fi
	@watch_pid=$$(pgrep -f "dotnet watch --project src/Grundet.Api/Grundet.Api.csproj" 2>/dev/null); \
	if [ -n "$$watch_pid" ]; then \
		echo "Killing backend dotnet watch process(es): $$watch_pid"; \
		kill $$watch_pid; \
	fi

_watch-backend:
	$(DEV_ENV) dotnet watch --project src/Grundet.Api/Grundet.Api.csproj run

_watch-frontend:
	cd src/Grundet.Web && NEXT_PUBLIC_API_URL=http://api.grundet.test:8080 pnpm dev

infra:
	docker compose -f infra/compose.dev.yml up -d

infra-down:
	docker compose -f infra/compose.dev.yml down

infra-reset:
	docker compose -f infra/compose.dev.yml down -v
	docker compose -f infra/compose.dev.yml up -d

ensure-ef:
	@dotnet ef --version >/dev/null 2>&1 || { \
		echo "dotnet-ef unavailable (likely a stale tool resolver cache from a deleted worktree); repairing..."; \
		rm -f "$$HOME/.dotnet/toolResolverCache/1/dotnet-ef"; \
		dotnet tool restore; \
	}

migrate: ensure-ef
	cd src/Grundet.Api && dotnet ef database update

seed-dev:
	@echo "Seeding approved alpha access for $(DEV_EMAIL) and $(DEV_SECOND_EMAIL)"
	@docker compose -f infra/compose.dev.yml exec -T postgres psql \
		-v ON_ERROR_STOP=1 \
		-v dev_email="$(DEV_EMAIL)" \
		-v dev_name="$(DEV_NAME)" \
		-v dev_second_email="$(DEV_SECOND_EMAIL)" \
		-v dev_second_name="$(DEV_SECOND_NAME)" \
		-U postgres -d grundet < scripts/seed-dev.sql

db-scenarios:
	@if [ -z "$(SCENARIOS)" ]; then \
		echo "No DB scenarios selected. Set SCENARIOS='billing/example billing/other' to apply dev-only scenario scripts."; \
	else \
		for scenario in $(SCENARIOS); do \
			case "$$scenario" in /*|*..*|*.sql) \
				echo "Invalid DB scenario name: $$scenario"; \
				exit 1; \
			esac; \
			path="scripts/db/scenarios/$$scenario.sql"; \
			if [ ! -f "$$path" ]; then \
				echo "Missing DB scenario: $$path"; \
				exit 1; \
			fi; \
			echo "Applying DB scenario: $$scenario"; \
			docker compose -f infra/compose.dev.yml exec -T postgres psql \
				-v ON_ERROR_STOP=1 \
				-U postgres -d grundet < "$$path"; \
		done; \
	fi

backend: check-backend-port
	$(DEV_ENV) dotnet watch --project src/Grundet.Api/Grundet.Api.csproj run

frontend:
	cd src/Grundet.Web && NEXT_PUBLIC_API_URL=http://api.grundet.test:8080 pnpm dev

install:
	cd src/Grundet.Web && pnpm install

generate-api:
	@echo "Backend must be running (make dev or make backend) before generating."
	cd src/Grundet.Web && pnpm run generate:api

test: test-backend test-frontend

test-backend:
	scripts/verify-backend.sh

test-frontend:
	scripts/verify-frontend.sh

# Build the downloadable Grundet skill bundle (tools/skills-gen/dist/grundet-<version>.zip).
skills:
	cd tools/skills-gen && pnpm install && pnpm run build

# Validate the skill source (frontmatter, includes) without writing a zip.
skills-validate:
	cd tools/skills-gen && pnpm install && pnpm run validate
