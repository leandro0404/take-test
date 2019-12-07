FROM microsoft/dotnet:2.1-runtime-deps

WORKDIR /app
COPY . ./

EXPOSE 5000/tcp
ENV ASPNETCORE_URLS http://*:5000

ENTRYPOINT ["./take_net_prova"]
