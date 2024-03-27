FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080/
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./*.sln ./
COPY */*.csproj ./
RUN for file in $(ls *.csproj); do echo ${file%.} && mkdir -p ${file%.*}/ && mv $file ${file%.*}/; done

RUN dotnet restore
COPY . .
WORKDIR "/src/Janitor.Core"
RUN dotnet build "Janitor.Core.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Janitor.Core.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

RUN useradd --uid 10000 jantitor 
USER 10000

ENTRYPOINT ["dotnet", "Janitor.Core.dll"]