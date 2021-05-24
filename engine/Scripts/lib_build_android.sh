work_path=$(pwd)
engine_path=
platform=
gn_params=""
optimize=""
ninja_params=""
runtime_mode=


echo "setting environment variable and other params..."
while getopts ":r:p:m:eo" opt
do
    case $opt in
        r)
        engine_path=$OPTARG # set engine_path, depot_tools and flutter engine folder will be put into this path
        ;;
        p)
        gn_params="$gn_params --$OPTARG" # set the target platform android/ios
        ;;
        m)
        runtime_mode=$OPTARG
        gn_params="$gn_params --runtime-mode=$runtime_mode" # set runtime mode release/debug
        ;;
        ?)
        echo "unknown param"
        exit 1;;
    esac
done

if [ "$runtime_mode" == "release" ];
then
  optimize=""
  output_path="android_release"
  ninja_params="-C out/android_release flutter/third_party/txt:txt_lib"
elif [ "$runtime_mode" == "debug" ];
then
  optimize="--unoptimized"
  output_path="android_debug_unopt"
  ninja_params=" -C out/android_debug_unopt flutter/third_party/txt:txt_lib"
elif [ "$runtime_mode" == "profile" ];
then
  echo "not support profile build"
  exit 1
fi

gn_params="$gn_params $optimize"

#set environment variable
function isexist()
{
    source_str=$1
    test_str=$2
    
    strings=$(echo $source_str | sed 's/:/ /g')
    for str in $strings
    do  
        if [ $test_str = $str ]; then
            return 0
        fi  
    done
    return 1
}

if [ ! $FLUTTER_ROOT_PATH ];then
  echo "export FLUTTER_ROOT_PATH=$engine_path/engine/src" >> ~/.bash_profile
else
  echo "This environment variable has been set, skip"
fi

if isexist $PATH $engine_path/depot_tools; then 
  echo "This environment variable has been set, skip"
else 
  echo "export PATH=$engine_path/depot_tools:\$PATH" >> ~/.bash_profile
fi
source ~/.bash_profile

echo "\nGetting Depot Tools..." 
if [ ! -n "$engine_path" ]; then   
  echo "Flutter engine path is not exist, please set the path by using \"-r\" param to set a engine path."  
  exit 1
fi
cd $engine_path	
if [ -d 'depot_tools' ] && [ -d "depot_tools/.git" ];
then
  echo "depot_tools already installed, skip"
else
  git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git
  gclient
fi

echo "\nGetting flutter engine..."

if [ -d 'engine' ];
then
  echo "engine folder already exist, skip"
else
  mkdir engine
fi
cd engine
echo "solutions = [
  {
    \"managed\": False,
    \"name\": \"src/flutter\",
    \"url\": \"git@github.com:flutter/engine.git\", 
    \"custom_deps\": {},
    \"deps_file\": \"DEPS\",
    \"safesync_url\": \"\",
  },
]" > .gclient

gclient sync

cd $FLUTTER_ROOT_PATH/flutter
git checkout flutter-1.17-candidate.5
gclient sync -D

echo "\nSCompiling engine..."
#apply patch to Build.gn
cd $FLUTTER_ROOT_PATH/flutter/third_party/txt
cp -f $work_path/patches/android/BUILD.gn.patch BUILD.gn.patch
patch < BUILD.gn.patch -N

cd $FLUTTER_ROOT_PATH/build/secondary/third_party/libcxxabi
cp -f $work_path/patches/android/BUILD_2.gn.patch BUILD_2.gn.patch
patch < BUILD_2.gn.patch -N

cd $FLUTTER_ROOT_PATH/build/mac
cp -f $work_path/patches/find_sdk.patch find_sdk.patch
patch < find_sdk.patch -N

cd $FLUTTER_ROOT_PATH
./flutter/tools/gn $gn_params

ninja $ninja_params

echo "\nStarting build engine..."
cd $work_path/../

python $FLUTTER_ROOT_PATH/flutter/sky/tools/objcopy.py --objcopy $FLUTTER_ROOT_PATH/third_party/android_tools/ndk/toolchains/arm-linux-androideabi-4.9/prebuilt/darwin-x86_64/bin/arm-linux-androideabi-objcopy --input $FLUTTER_ROOT_PATH/third_party/icu/flutter/icudtl.dat --output icudtl.o --arch arm

if [ -d '../com.unity.uiwidgets/Runtime/Plugins/android' ];
then
  echo "android folder already exist, skip create folder"
else
  mkdir ../com.unity.uiwidgets/Runtime/Plugins/android
fi

if [ "$runtime_mode" == "release" ];
then
  mono bee.exe android_release
  rm -rf ../com.unity.uiwidgets/Runtime/Plugins/android/libUIWidgets.so

  mkdir $work_path/../artifacts/rsp/backup
  cp -f $work_path/../artifacts/rsp/14590475716575637239.rsp $work_path/../artifacts/rsp/backup/14590475716575637239.rsp
  cd $work_path
  python -c 'import rsppatch; print rsppatch.rsp_patch()' 

  cd $work_path/../
  "artifacts/Stevedore/android-ndk-mac/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang++" @"artifacts/rsp/14590475716575637239.rsp"
  $FLUTTER_ROOT_PATH/buildtools/mac-x64/clang/bin/clang++ @"artifacts/rsp/14590475716575637239.rsp"
  cp artifacts/libUIWidgets/release_Android_arm32/libUIWidgets.so ../com.unity.uiwidgets/Runtime/Plugins/Android/libUIWidgets.so

elif [ "$runtime_mode" == "debug" ];
then
  mono bee.exe android_debug
  rm -rf ../com.unity.uiwidgets/Runtime/Plugins/android/libUIWidgets.so
  cp -r build_debug/. ../com.unity.uiwidgets/Runtime/Plugins/android
fi

echo "\nRevert patches..."
cd $FLUTTER_ROOT_PATH/flutter/third_party/txt
patch -R < BUILD.gn.patch

cd $FLUTTER_ROOT_PATH/build/secondary/third_party/libcxxabi
patch -R < BUILD_2.gn.patch

cd $FLUTTER_ROOT_PATH/build/mac
patch -R < find_sdk.patch

if [ "$runtime_mode" == "release" ];
then
  rm -rf $work_path/../artifacts/rsp/14590475716575637239.rsp
  cp -f $work_path/../artifacts/rsp/backup/14590475716575637239.rsp $work_path/../artifacts/rsp/14590475716575637239.rsp
  rm -rf $work_path/../artifacts/rsp/backup
  echo "rsp reverted"
fi