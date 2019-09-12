FROM czifro/paket-fake-build:5.10.0.160--2.0.5-2.1.4-jessie

WORKDIR /app-build

RUN mkdir .paket && mkdir FsSql && mkdir FsSql.Tests

COPY FsSql.sln paket-install.sh paket.dependencies global.json ./
COPY FsSql/FsSql.fsproj FsSql/paket.references ./FsSql/
COPY FsSql.Tests/FsSql.Tests.fsproj FsSql.Tests/paket.references ./FsSql.Tests/

RUN sh paket-install && dotnet restore

COPY FsSql/*.fs ./FsSql/
COPY FsSql.Tests/*.fs ./FsSql.Test/

COPY RELEASE_NOTES.md build.fsx build.sh ./

RUN sh build.sh