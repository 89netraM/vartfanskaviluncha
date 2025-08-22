FROM mcr.microsoft.com/dotnet/sdk:9.0@sha256:ae000be75dac94fc40e00f0eee903289e985995cc06dac3937469254ce5b60b6 AS build

WORKDIR /app

RUN apt-get update && apt-get install -y build-essential curl && apt-get update
RUN curl https://sh.rustup.rs -sSf | bash -s -- -y
ENV PATH="/root/.cargo/bin:${PATH}"

COPY ./OpeningHoursParser/OpeningHoursParser.csproj ./OpeningHoursParser/
COPY ./OpeningHoursParser/packages.lock.json ./OpeningHoursParser/
COPY ./OpeningHoursParser/Cargo.toml ./OpeningHoursParser/
COPY ./OpeningHoursParser/Cargo.lock ./OpeningHoursParser/
RUN mkdir ./OpeningHoursParser/src && touch ./OpeningHoursParser/src/lib.rs
RUN cargo build --release --locked --manifest-path ./OpeningHoursParser/Cargo.toml

COPY ./VartFanSkaViLuncha.slnx ./global.json ./Directory.Packages.props ./Directory.Build.props ./.gitignore ./.editorconfig ./

COPY ./ServiceDefaults/ServiceDefaults.csproj ./ServiceDefaults/packages.lock.json ./ServiceDefaults/
COPY ./Web/Web.csproj ./Web/packages.lock.json ./Web/

RUN dotnet restore ./Web/Web.csproj --locked-mode

COPY ./OpeningHoursParser ./OpeningHoursParser
COPY ./ServiceDefaults ./ServiceDefaults
COPY ./Web ./Web

RUN dotnet build ./Web/Web.csproj --no-restore --configuration Release

RUN dotnet publish ./Web/Web.csproj --no-build --configuration Release --output /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0@sha256:515c08e27cf8d933af442f0e1b6c92cc728acb71f113ca529f56c79cb597adb5 AS release

WORKDIR /app

COPY --from=build /out ./

ENTRYPOINT ["./VartFanSkaViLuncha.Web"]
