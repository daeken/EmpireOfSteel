FROM ubuntu:20.04

WORKDIR /

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get -y install build-essential clang git python2.7 python3

RUN git clone --branch releng/13.1 --depth 1 https://github.com/freebsd/freebsd-src.git

RUN DEBIAN_FRONTEND=noninteractive apt-get -y install lld libarchive-dev libbz2-dev

WORKDIR /freebsd-src

RUN mkdir /build
RUN MAKEOBJDIRPREFIX=/build XCC=/usr/bin/clang XCXX=/usr/bin/clang++ XCPP=/usr/bin/clang-cpp XLD=/usr/bin/ld.lld ./tools/build/make.py TARGET=amd64 TARGET_ARCH=amd64 kernel-toolchain -j`nproc`

WORKDIR /
COPY . .
RUN cp /PARTTIMEGOD /freebsd-src/sys/amd64/conf/

WORKDIR /freebsd-src
RUN git apply /patches.diff --ignore-space-change --ignore-whitespace
RUN MAKEOBJDIRPREFIX=/build XCC=/usr/bin/clang XCXX=/usr/bin/clang++ XCPP=/usr/bin/clang-cpp XLD=/usr/bin/ld.lld ./tools/build/make.py TARGET=amd64 TARGET_ARCH=amd64 KERNCONF=PARTTIMEGOD NO_MODULES=yes buildkernel -j`nproc`
