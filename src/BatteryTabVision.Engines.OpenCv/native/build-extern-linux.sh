#!/usr/bin/env bash
# Build libOpenCvSharpExtern.so for Ubuntu 24.04
# Requires: cmake, g++, libopencv-dev, libopencv-contrib-dev
set -e
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

apt-get install -y cmake g++ libopencv-dev libopencv-contrib-dev

git clone --depth 1 --filter=blob:none --no-checkout https://github.com/shimat/opencvsharp /tmp/opencvsharp-src
cd /tmp/opencvsharp-src
git sparse-checkout set src/OpenCvSharpExtern
git checkout tags/4.6.0.20220608 -- src/OpenCvSharpExtern/

# Patch: remove headers not available in Ubuntu 24.04 apt
sed -i 's|#include <opencv2/xfeatures2d.hpp>|// #include <opencv2/xfeatures2d.hpp>|' src/OpenCvSharpExtern/include_opencv.h

# Build with problematic modules excluded
cat > src/OpenCvSharpExtern/CMakeLists.txt << 'CMAKE'
cmake_minimum_required(VERSION 3.0)
file(GLOB OPENCVSHARP_FILES *.cpp)
list(REMOVE_ITEM OPENCVSHARP_FILES
    ${CMAKE_CURRENT_SOURCE_DIR}/xfeatures2d.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/barcode.cpp
    ${CMAKE_CURRENT_SOURCE_DIR}/saliency.cpp)
find_package(OpenCV REQUIRED)
if(OpenCV_FOUND)
    include_directories(${OpenCV_INCLUDE_DIRS})
    add_compile_definitions(NO_BARCODE)
    add_library(OpenCvSharpExtern SHARED ${OPENCVSHARP_FILES})
    target_link_libraries(OpenCvSharpExtern ${OpenCV_LIBRARIES})
    install(TARGETS OpenCvSharpExtern LIBRARY DESTINATION lib)
endif()
CMAKE

mkdir -p /tmp/opencvsharp-build && cd /tmp/opencvsharp-build
cmake /tmp/opencvsharp-src/src/OpenCvSharpExtern -DOpenCV_DIR=/usr/lib/x86_64-linux-gnu/cmake/opencv4 -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)
cp libOpenCvSharpExtern.so /usr/local/lib/
ldconfig
cp libOpenCvSharpExtern.so "$SCRIPT_DIR/linux-x64/"
echo "Done: libOpenCvSharpExtern.so installed to /usr/local/lib/ and $SCRIPT_DIR/linux-x64/"
