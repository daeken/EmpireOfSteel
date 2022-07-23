#!/bin/bash

mkdir -p obj
docker build -t ptg .
docker run -v `pwd`/obj:/output ptg cp /build/freebsd-src/amd64.amd64/sys/PARTTIMEGOD/kernel.full /output
