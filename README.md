# Biletado API -- Web Engineering 2 Backend

Repository:\
https://github.com/HeinrichderAchte/biletadoapiwebeng.git

Backend-Implementierung einer REST-API im Rahmen der Aufgabenstellung
**Web Engineering 2** (DHBW Karlsruhe, TINF23B3).

Der Service stellt eine HTTP-basierte Schnittstelle bereit, integriert
sich in die vorgegebene **Biletado-Kubernetes-Entwicklungsumgebung** und
bietet eine **Rapidoc UI** zur interaktiven API-Dokumentation.

Framework: **ASP.NET Core (.NET)**\
Service-Port: **9000**\
Autoren: Henri Weber; Vivian Heidt
Lizenz: MIT (siehe LICENSE)

Ziel der Abgabe ist, dass die API als **Container lauffähig** ist, sich
in die bereitgestellte Kubernetes-Umgebung integrieren laesst und wie
spezifiziert reagiert.

------------------------------------------------------------------------

# Inhalt

-   Implementierte Endpunkte\
-   Swagger / API-Dokumentation\
-   Konfiguration\
-   Authentifizierung\
-   Code-Ueberblick\
-   Lokales Setup (ohne Kubernetes)\
-   Container Build & Run\
-   Lokales Setup mit kind + Kubernetes\
-   CI / Build & Testautomation\
-   Logging\
-   Lizenz

------------------------------------------------------------------------

# Implementierte Endpunkte

Die API stellt fachliche REST-Endpunkte sowie Status- und
Health-Endpunkte bereit.\

## Status / Health

-   `GET api/v3/reservations/status`\
-   `GET api/v3/reservations/healt`\
-   `GET api/v3/reservations/live`\
-   `GET api/v3/reservations/ready`

## Reservations API


-   `GET /api/...` -- Lesen von allen Reservierungen\
-   `POST /api/.../{id}` -- anlegen einer Reservierung\
-   `GET /api/...` -- Reservierung anhand einer ID lesen\
-   `PUT /api/.../{id}` -- Aktualisieren / Widerherstellen einer Reservierung\
-   `DELETE /api/.../{id}` -- Soft Delete/ Hard Delete einer Reservierung 

------------------------------------------------------------------------

# API-Dokumentation

Die API bringt eine integrierte Rapidoc UI mit.

Nach lokalem Start ist sie erreichbar unter:

    http://localhost:9090/rapidoc/reservations-v3/

------------------------------------------------------------------------

# Konfiguration

Die Konfiguration erfolgt zur Laufzeit ueber **Umgebungsvariablen** und
ggf. ueber Kubernetes-Manifeste (Kustomize).

Typische Parameter:

ASPNETCORE_ENVIRONMENT, ASPNETCORE_URLS, LOG_LEVEL, DB_HOST, DB_PORT,
DB_NAME, DB_USER, DB_PASSWORD

------------------------------------------------------------------------

# Authentifizierung

In der aktuellen Implementierung ist **keine JWT-basierte
Authentifizierung aktiv**.

Alle Endpunkte sind derzeit **ohne Authentifizierung** erreichbar.

Die API ist jedoch so aufgebaut, dass eine spaetere Erweiterung um
JWT-Validierung moeglich ist.

------------------------------------------------------------------------

# Code-Ueberblick

-   `Program.cs` -- Einstiegspunkt, Konfiguration, Middleware\
-   `Controllers/` -- REST-Endpunkte, Request-Handling, Validierung\
-   `DTOs/` -- Data Transfer Objects\
-   `Models/` -- Datenmodelle der Reservierungen, Räume etc.\
-   `Persistence/Context/` -- Datenbankmapping\
-   `Repository/DevAuth/` -- Authentifizierung (momentan noch unvollständig)
-   `Services/` -- Business-Logik

------------------------------------------------------------------------

# Lokales Setup (ohne Kubernetes)

``` cmd
cd Biletado
dotnet restore
dotnet run
```

Swagger: http://localhost:9090/rapidoc/reservations-v3/

------------------------------------------------------------------------

# Container Build & Run

``` cmd
docker build -f Biletado\Dockerfile -t biletadoapi:dev .
docker run -p 9000:9000 biletadoapi:dev
```

------------------------------------------------------------------------

# Lokales Setup mit kind + Kubernetes

``` cmd
kind create cluster --name biletado
kubectl create namespace biletado
kubectl config set-context --current --namespace biletado
kubectl apply -k "https://gitlab.com/biletado/kustomize.git//overlays/kind?ref=main"
kubectl apply -k Biletado
kubectl port-forward -n biletado svc/biletado 9000:9000
```

------------------------------------------------------------------------

# CI / Build & Testautomation

Das Repository enthaelt eine GitHub-Actions-Pipeline fuer Build und
optionale Tests.

------------------------------------------------------------------------

# Logging

Logging erfolgt ueber das Serilog (Dokumentation: https://github.com/serilog/serilog/wiki). Das Loglevel ist
konfigurierbar.

------------------------------------------------------------------------

# Lizenz

MIT License -- siehe LICENSE
