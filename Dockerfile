ARG CERT_PASSWORD_ARG=3vo3rmb5DBJXsryjMfJsrpjbKsbj8B
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine-amd64 as build-env
ARG CERT_PASSWORD_ARG
ENV CERT_PASSWORD=$CERT_PASSWORD_ARG
WORKDIR /App
COPY . ./
RUN dotnet restore \
	&& dotnet publish TelebilbaoEpg/TelebilbaoEpg.csproj --no-restore --self-contained false -c Release -o out /p:UseAppHost=false \
	&& dotnet dev-certs https --export-path /config/aspnetapp.pem --password "$CERT_PASSWORD" --format PEM \
	&& rm **/appsettings.Development.json && rm **/*.pdb

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine-amd64
ARG CERT_PASSWORD_ARG
ENV CERT_PASSWORD=$CERT_PASSWORD_ARG
WORKDIR /App
RUN apk update \
    && apk upgrade --available \
	&& apk add ca-certificates \
	&& apk add  tzdata \
	&& apk add envsubst \
	&& apk add bash \
	&& mkdir /config && mkdir -p /usr/local/share/ca-certificates/
COPY --from=build-env /App/out . 
COPY --from=build-env /config /config
ENV DOTNET_CLI_TELEMETRY_OPTOUT=1
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:80;https://+:443
ENV ASPNETCORE_Kestrel__Certificates__Default__Path=/usr/local/share/ca-certificates/aspnetapp.crt
ENV ASPNETCORE_Kestrel__Certificates__Default__KeyPath=/usr/local/share/ca-certificates/aspnetapp.key
ENV ASPNETCORE_Kestrel__Certificates__Default__Password=$CERT_PASSWORD
ENV JOB_SCHEDULE="0 0/30 * * * ?"
ENV MOVIE_API_URL=https://api.themoviedb.org/
ENV MOVIE_API_KEY=""
ENV MOVIE_IMAGE_URL=https://image.tmdb.org
RUN chown -R app:app /App/* \
    && cp /config/aspnetapp.pem $ASPNETCORE_Kestrel__Certificates__Default__Path \
	&& cp /config/aspnetapp.key $ASPNETCORE_Kestrel__Certificates__Default__KeyPath \
    && chmod 755 $ASPNETCORE_Kestrel__Certificates__Default__Path && chmod +x $ASPNETCORE_Kestrel__Certificates__Default__Path \
	&& chown app:app $ASPNETCORE_Kestrel__Certificates__Default__Path  \
    && cat $ASPNETCORE_Kestrel__Certificates__Default__Path >> /etc/ssl/certs/ca-certificates.crt \
    && chmod 755 $ASPNETCORE_Kestrel__Certificates__Default__KeyPath && chmod +x $ASPNETCORE_Kestrel__Certificates__Default__KeyPath \
	&& chown app:app $ASPNETCORE_Kestrel__Certificates__Default__KeyPath \
    && rm -rf /tmp && mkdir /tmp && chmod 755 /tmp && chown app:app /tmp \
    && update-ca-certificates \
	&& rm -rf /config \
	&& rm -rf /var/cache/apk/* \
    && mkdir /data && chmod 755 /data \
	&& cat > /data/telebilbaoEpg.db  \
    && chmod 777 /data/telebilbaoEpg.db \
	&& chown -R app:app /data/* \
ENTRYPOINT echo "$(envsubst '${MOVIE_API_URL},${MOVIE_API_KEY},${MOVIE_IMAGE_URL},${$JOB_SCHEDULE}' < appsettings.json)" > appsettings.json \
			&& dotnet "TelebilbaoEpg.dll"
EXPOSE 80 
EXPOSE 443