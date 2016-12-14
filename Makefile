
BASE_SRC=src
#OS=linux_x64
OS=macosx_x64

# Workaround to Nuget issue: http://www.grpc.io/docs/quickstart/csharp.html
TOOLS_PATH=$(BASE_SRC)/packages/Grpc.Tools.1.0.0/tmp/packages/Grpc.Tools.1.0.0/tools/$(OS)
SERVER=$(BASE_SRC)/server
CLIENT=$(BASE_SRC)/client
SERVICE_CODEGEN=$(BASE_SRC)/Service
COMPOSE_FILE=$(SERVER)/bin/debug/netcoreapp1.1/publish/docker-compose.yml
PORT=8080
DOCKER_PORT=50051

proto_codegen:
	$(TOOLS_PATH)/protoc -I$(BASE_SRC)/protos --csharp_out $(SERVICE_CODEGEN) --grpc_out $(SERVICE_CODEGEN) $(BASE_SRC)/protos/service.proto --plugin=protoc-gen-grpc=$(TOOLS_PATH)/grpc_csharp_plugin

restore_service: proto_codegen
	cd $(SERVICE_CODEGEN) && dotnet restore &&  cd ../../

restore_server: proto_codegen restore_service
	cd $(SERVER) && dotnet restore &&  cd ../../

build_server: restore_server
	cd $(SERVER) && dotnet build &&  cd ../../

restore_client: proto_codegen restore_service
	cd $(CLIENT) && dotnet restore &&  cd ../..

run_server: restore_server
	cd $(SERVER) && dotnet run $(PORT) && cd ../..

run_client: restore_client
	cd $(CLIENT) && dotnet run $(DOCKER_PORT) && cd ../../

publish_servers: restore_server
	cd $(SERVER) && dotnet publish -o bin/debug/netcoreapp1.1/publish && cd ../../

dockerize_servers: publish_servers
	docker-compose -f $(COMPOSE_FILE) build --force-rm

docker_run_servers: dockerize_servers
	docker-compose -f $(COMPOSE_FILE) up -d

docker_stop_all:
	docker-compose -f $(COMPOSE_FILE) down

docker_logs:
	docker-compose -f $(COMPOSE_FILE) logs
