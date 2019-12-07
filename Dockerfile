FROM microsoft/dotnet:2.1-runtime-deps

WORKDIR /app
COPY . ./

EXPOSE 5000/tcp
ENV ASPNETCORE_URLS http://*:8080

ENTRYPOINT ["./Take.Processo.Seletivo"]
