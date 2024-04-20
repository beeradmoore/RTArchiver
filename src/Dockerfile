FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.19-amd64 AS build-env

WORKDIR /app

#ARG DEBIAN_FRONTEND=noninteractive
#ENV TZ=Australia/Melbourne


#RUN apt-get update && \
#    apt-get -y upgrade && \
#    apt-get -y install intel-media-va-driver-non-free vainfo intel-gpu-tools && \
#    apt-get -y install ffmpeg curl wget 

#RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp && \
#    chmod a+rx /usr/local/bin/yt-dlp

# Copy everything
COPY RTArchiver/ ./
# Restore as distinct layers
RUN dotnet restore
# Build and publish a release
RUN dotnet publish -c Release -o out


# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine3.19-amd64

WORKDIR /app

RUN apk update && \
    apk upgrade && \
    apk add ffmpeg libva-intel-driver libva-utils intel-media-driver && \
    apk add python3 curl

RUN curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/bin/yt-dlp && \
    chmod a+rx /usr/bin/yt-dlp

#COPY test_scripts/test.sh ./
#ENTRYPOINT ["tail", "-f", "/dev/null"]

COPY --from=build-env /app/out .
ENTRYPOINT ["/app/RTArchiver"]
#ENTRYPOINT ["tail", "-f", "/dev/null"]