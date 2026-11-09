# Acesse https://aka.ms/customizecontainer para saber como personalizar seu contêiner de depuração e como o Visual Studio usa este Dockerfile para criar suas imagens para uma depuração mais rápida.

# Esta fase é usada durante a execução no VS no modo rápido (Padrão para a configuração de Depuração)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER root
# As bibliotecas nativas do PaddleOCR (Paddle Inference e OpenCV) vêm nos pacotes NuGet de runtime linux-x64.
# O Paddle Inference ainda precisa do OpenMP do sistema (libgomp1), e as bibliotecas dele se carregam entre si
# pelo nome, sem caminho: o LD_LIBRARY_PATH aponta para a pasta onde o publish as coloca.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libgomp1 \
    && rm -rf /var/lib/apt/lists/*
ENV LD_LIBRARY_PATH=/app/runtimes/linux-x64/native
# Comprovantes enviados (OcrSettings:StorageBasePath). O contêiner roda sem root,
# então a pasta precisa pertencer ao usuário da aplicação. Monte um volume aqui
# para não perder os arquivos ao recriar o contêiner.
RUN mkdir -p /app/storage/documents && chown -R $APP_UID /app/storage
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# Esta fase é usada para compilar o projeto de serviço
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copia só os arquivos de projeto antes do restore para aproveitar o cache das camadas.
COPY ["AppFinances-API/AppFinances-API.csproj", "AppFinances-API/"]
COPY ["AppFinancesClassLibrary/AppFinancesClassLibrary.csproj", "AppFinancesClassLibrary/"]
RUN dotnet restore "./AppFinances-API/AppFinances-API.csproj"
COPY . .
WORKDIR "/src/AppFinances-API"
RUN dotnet build "./AppFinances-API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./AppFinances-API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "AppFinances-API.dll"]
