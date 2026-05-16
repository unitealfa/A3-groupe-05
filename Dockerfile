FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . .

RUN dotnet publish ./EasySave.Console/EasySave.Console.csproj -c Release -o /app/publish --verbosity minimal

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish/ ./
COPY docker/entrypoint.sh /entrypoint.sh

RUN chmod +x /entrypoint.sh \
    && mkdir -p /app/config /app/logs /app/state /workspace/source /workspace/target

ENTRYPOINT ["/entrypoint.sh"]
