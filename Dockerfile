FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore src/IHaveBeen.Web/IHaveBeen.Web.csproj --locked-mode
RUN dotnet publish src/IHaveBeen.Web/IHaveBeen.Web.csproj -c Release --no-restore -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
ARG SOURCE_URL
LABEL org.opencontainers.image.source=$SOURCE_URL
RUN mkdir -p /var/lib/i-have-been/keys /var/lib/i-have-been/quarantine && chmod 700 /var/lib/i-have-been/quarantine && chown -R app:app /var/lib/i-have-been
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
ENV ASPNETCORE_TEMP=/var/lib/i-have-been/quarantine TMPDIR=/var/lib/i-have-been/quarantine
EXPOSE 8080
ENTRYPOINT ["dotnet", "IHaveBeen.Web.dll"]
