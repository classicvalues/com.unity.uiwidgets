work_path=$(pwd)
engine_path=
platform=
gn_params=""
optimize="--unoptimized"
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
        gn_params="$gn_params --$OPTARG" # set the target platform android/ios/linux
        ;;
        m)
        runtime_mode=$OPTARG
        gn_params="$gn_params --runtime-mode=$runtime_mode" # set runtime mode release/debug/profile
        ;;
        e)
        gn_params="$gn_params --bitcode" # enable-bitcode switch
        ;;
        o)
        optimize="" # optimize code switch
        ;;
        ?)
        echo "unknown param"
        exit 1;;
    esac
done

if [ "$runtime_mode" == "release" ] && [ "$optimize" == "--unoptimized" ];
then
  ninja_params=" -C out/host_release_unopt flutter/third_party/txt:txt_lib"
elif [ "$runtime_mode" == "release" ] && [ "$optimize" == "" ];
then
  echo $ninja_params
  ninja_params="-C out/host_release flutter/third_party/txt:txt_lib"
  echo $ninja_params
elif [ "$runtime_mode" == "debug" ] && [ "$optimize" == "--unoptimized" ];
then
  ninja_params=" -C out/host_debug_unopt flutter/third_party/txt:txt_lib"
elif [ "$runtime_mode" == "debug" ] && [ "$optimize" == "" ];
then
  ninja_params=" -C out/host_debug flutter/third_party/txt:txt_lib"
elif [ "$runtime_mode" == "profile" ];
then
  echo "not support profile build yet"
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
#git@github.com:guanghuispark/engine.git is a temp repo, replace it later
echo "solutions = [
  {
    \"managed\": False,
    \"name\": \"src/flutter\",
    \"url\": \"git@github.com:guanghuispark/engine.git\", 
    \"custom_deps\": {},
    \"deps_file\": \"DEPS\",
    \"safesync_url\": \"\",
  },
]" > .gclient

gclient sync

cd src/flutter
git checkout flutter-1.17-candidate.5
gclient sync -D

echo "\nSCompiling engine..."
#apply patch to Build.gn
cd third_party/txt
cp -f $work_path/patches/BUILD.gn.patch BUILD.gn.patch
patch < BUILD.gn.patch -N

cd $engine_path/engine/src/build/mac
cp -f $work_path/patches/find_sdk.patch find_sdk.patch
patch < find_sdk.patch -N
cd ../..
./flutter/tools/gn $gn_params

if [ "$runtime_mode" == "release" ] && [ "$optimize" == "--unoptimized" ];
then
  echo "icu_use_data_file=false" >> out/host_release_unopt/args.gn
elif [ "$runtime_mode" == "release" ] && [ "$optimize" == "" ];
then
  echo "icu_use_data_file=false" >> out/host_release/args.gn
elif [ "$runtime_mode" == "debug" ] && [ "$optimize" == "--unoptimized" ];
then
  echo "icu_use_data_file=false" >> out/host_debug_unopt/args.gn
elif [ "$runtime_mode" == "debug" ] && [ "$optimize" == "" ];
then
  echo "icu_use_data_file=false" >> out/host_debug/args.gn
elif [ "$runtime_mode" == "profile" ];
then
  echo "not support profile build yet"
  exit 1
fi

ninja $ninja_params

echo "\nStarting build engine..."
#run mono
cd $work_path
cd ..
if [ "$runtime_mode" == "release" ];
then
  cp -f $work_path/patches/bee_release.patch bee_release.patch
  patch < bee_release.patch -N
elif [ "$runtime_mode" == "debug" ];
then
  cp -f $work_path/patches/bee_debug.patch bee_debug.patch
  patch < bee_debug.patch -N
fi

mono bee.exe mac