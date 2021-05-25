from genericpath import exists
from pathlib import Path
import os
import sys
import getopt
import shutil
import json

work_path=os.getcwd()
engine_path=""
platform=""
gn_params=""
optimize=""
ninja_params=""
ninja_params1=""
ninja_params2=""
ninja_params3=""
runtime_mode=""
bitcode=""
flutter_root_path=""
visual_studio_path=""

def get_opts():
    # get intput agrs
    global engine_path
    global gn_params
    global runtime_mode
    global bitcode
    global visual_studio_path
    global platform

    options, args = getopt.getopt(sys.argv[1:], 'r:p:m:v:e')
    for opt, arg in options:
        if opt == '-r':
            engine_path = arg # set engine_path, depot_tools and flutter engine folder will be put into this path
        elif opt == '-p':
            platform = arg
            if platform == "android" or platform == "ios":
                gn_params += gn_params + " --" + arg # set the target platform android/ios
        elif opt == '-m':
            runtime_mode = arg
            gn_params += gn_params + " --runtime-mode=" + runtime_mode # set runtime mode release/debug
        elif opt == '-v':
            visual_studio_path = arg
        elif opt == '-e':
            bitcode="-bitcode_bundle -bitcode_verify"
            gn_params = gn_params + " --bitcode" # enable-bitcode switch
        
def engine_path_check():
    global engine_path
    if not os.path.exists(engine_path):
        os.makedirs(engine_path)

def bitcode_conf():
    global platform
    global bitcode

    if platform == "ios": 
        f = open("bitcode.conf",'w')
        if bitcode == "-bitcode_bundle -bitcode_verify":
            f.write("true")
        else:
            f.write("false")
        f.close()


def set_params():
    global output_path
    global ninja_params
    global ninja_params1
    global ninja_params2
    global ninja_params3
    global gn_params
    global visual_studio_path
    global platform
    global optimize

    print("setting environment variable and other params...")
    if platform == "windows":
        visual_studio_path_env = os.getenv('GYP_MSVS_OVERRIDE_PATH', 'null')
        if visual_studio_path == "":
            if visual_studio_path_env == 'null':
                assert False, "In func set_params(), visual_studio_path is not exist, please set the path by using \"-v\" param to set a engine path."
        else:
            os.environ["GYP_MSVS_OVERRIDE_PATH"] = visual_studio_path
            
    if runtime_mode == "release" and (platform == "mac" or platform == "windows"):
        optimize=""
        output_path="host_release"
        ninja_params1=" -C out/" +output_path + " flutter/third_party/txt:txt_lib"
        ninja_params2=" -C out/" +output_path + " third_party/angle:angle_lib"
        ninja_params3=" -C out/" +output_path + " third_party/angle:libEGL_static"
    elif runtime_mode == "debug" and (platform == "mac" or platform == "windows"):
        optimize="--unoptimized"
        output_path="host_debug_unopt"
        ninja_params1=" -C out/" +output_path + " flutter/third_party/txt:txt_lib"
        ninja_params2=" -C out/" +output_path + " third_party/angle:angle_lib"
        ninja_params3=" -C out/" +output_path + " third_party/angle:libEGL_static"
    elif runtime_mode == "release" and platform == "android":
        optimize=""
        output_path="android_release"
    elif runtime_mode == "debug" and platform == "android":
        optimize="--unoptimized"
        output_path="android_debug_unopt"
    elif runtime_mode == "release" and platform == "ios":
        optimize=""
        output_path="ios_release"
    elif runtime_mode == "debug" and platform == "ios":
        optimize="--unoptimized"
        output_path="ios_debug_unopt"
    else:
        print(runtime_mode + "\t" + platform)
        assert False, "In func set_params(), unknown param"

    ninja_params=" -C out/" + output_path + " flutter/third_party/txt:txt_lib"
    gn_params=gn_params + " " + optimize

def set_env_verb():
    global flutter_root_path
    flutter_root_path = os.getenv('FLUTTER_ROOT_PATH', 'null')
    if flutter_root_path == 'null':
        os.environ["FLUTTER_ROOT_PATH"] = engine_path + "/engine/src"
        flutter_root_path = os.getenv('FLUTTER_ROOT_PATH')
    else:
        print("This environment variable has been set, skip")
    env_path = os.getenv('Path')
    path_strings = env_path.split(';')
    for path in path_strings:
        if path.startswith(engine_path):
            print("This environment variable has been set, skip")
            return
    os.environ["Path"] = engine_path + "/depot_tools;" + os.environ["Path"]

def get_depot_tools():
    print("\nGetting Depot Tools...")
    if not os.path.exists(engine_path):
        assert False,"Flutter engine path is not exist, please set the path by using \"-r\" param to set a engine path."
    if os.path.exists(Path(engine_path + "/depot_tools")) and os.path.exists(Path(engine_path + "/depot_tools/.git")):
        print("depot_tools already installed, skip")
    else:
        os.chdir(engine_path)
        os.system("git clone https://chromium.googlesource.com/chromium/tools/depot_tools.git")
        os.system("gclient")

def get_flutter_engine():
    global engine_path
    global flutter_root_path
    print("\nGetting flutter engine...")
    print(Path(engine_path + "/engine"))
    if not os.path.exists(Path(engine_path + "/engine")):
        os.makedirs(Path(engine_path + "/engine"))

    content = '''
solutions = [
{
    "managed": False,
    "name": "src/flutter",
    "url": "git@github.com:flutter/engine.git", 
    "custom_deps": {},
    "deps_file": "DEPS",
    "safesync_url": "",
},
]
'''
    f = open(Path(engine_path + "/engine/.gclient"), "w")
    f.write(content)
    f.close()
    os.chdir(Path(engine_path + "/engine"))
    os.system("gclient sync")
    os.chdir(Path(flutter_root_path + "/flutter"))
    os.system("git checkout flutter-1.17-candidate.5")
    os.system("gclient sync -D")

def compile_engine():
    global flutter_root_path
    global work_path
    global gn_params
    global output_path
    global ninja_params1
    global ninja_params2
    global ninja_params3
    global platform

    print("\nSCompiling engine...")

    os.chdir(Path(flutter_root_path + "/flutter/third_party/txt"))
    copy_file(Path(work_path + "/patches/BUILD.gn.patch"), Path(flutter_root_path + "/flutter/third_party/txt"))
    os.system("patch < BUILD.gn.patch -N")

    if platform == "ios" or platform == "mac":
        os.chdir(Path(flutter_root_path + "/build/mac"))
        copy_file(Path(work_path + "/patches/find_sdk.patch"), Path(flutter_root_path + "/build/mac"))
        os.system("patch < find_sdk.patch -N")
    elif platform == "android":
        os.chdir(Path(flutter_root_path + "/build/secondary/third_party/libcxxabi"))
        copy_file(Path(work_path + "/patches/android/BUILD_2.gn.patch"), Path(flutter_root_path + "/build/secondary/third_party/libcxxabi"))
        os.system("patch < BUILD_2.gn.patch -N")
    elif platform == "windows":
        os.chdir(Path(flutter_root_path + "/third_party/angle"))
        copy_file(Path(work_path + "/patches/windows/BUILD.gn.patch"), Path(flutter_root_path + "/third_party/angle"))
        os.system("patch < BUILD.gn.patch -N")

        os.chdir(Path(flutter_root_path + "/third_party/angle/src/libANGLE/renderer/d3d/d3d11/"))
        copy_file(Path(work_path + "/patches/windows/cpp.patch"), Path(flutter_root_path + "/third_party/angle/src/libANGLE/renderer/d3d/d3d11"))
        os.system("patch < cpp.patch -N")

        os.chdir(Path(flutter_root_path + "/third_party/skia/"))
        copy_file(Path(work_path + "/patches/windows/BUILD_2.gn.patch"), Path(flutter_root_path + "/third_party/skia"))
        os.system("patch < BUILD_2.gn.patch -N")

    os.chdir(Path(flutter_root_path))
    os.system("python ./flutter/tools/gn " + gn_params)

    if platform == "mac" or platform == "ios":
        f = open(Path(flutter_root_path + "/out/" + output_path + "/args.gn"), 'a')
        f.write("icu_use_data_file=false")
        f.close()
        os.system("ninja " + ninja_params)
    elif platform == "windows":
        f = open(Path(flutter_root_path + "/out/" + output_path + "/args.gn"), 'a')
        f.write("skia_use_angle = true\nskia_use_egl = true")
        f.close()
        os.system("ninja " + ninja_params1)
        os.system("ninja " + ninja_params2)
        os.system("ninja " + ninja_params3)
        os.chdir(Path(flutter_root_path + "/third_party/icu/flutter/"))
        os.system("ld -r -b binary -o icudtl.o icudtl.dat")
    elif platform == "android":
        os.system("ninja " + ninja_params)

def build_engine():
    global flutter_root_path
    global work_path
    global runtime_mode
    global platform
    global output_path
    global bitcode
    dest_folder=""

    print("\nStarting build engine...")
    if platform == "windows":
        dest_folder = "x86_64"
    elif platform == "mac":
        dest_folder = "osx"
    elif platform == "android":
        dest_folder = "android"
    elif platform == "ios":
        dest_folder = "ios"
    if not os.path.exists(Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/" + dest_folder)):
        os.makedirs(Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/" + dest_folder))
    os.chdir(Path(work_path + "/../"))
    if runtime_mode == "release":
        os.system("rm -rf build_release/*")
        if platform == "windows":
            os.system("bee.exe win_release")
            copy_file(Path(work_path + "/../build_release/"), Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/" + dest_folder))
        else:
            os.system("mono bee.exe " + platform +"_release")
            copy_file(Path(work_path + "/../build_release/"), Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/" + dest_folder))
            if platform == "android":
                os.makedirs(Path(work_path + "/../artifacts/rsp/backup"))
                copy_file(Path(work_path + "/../artifacts/rsp/14590475716575637239.rsp"), Path(work_path + "/../artifacts/rsp/backup/14590475716575637239.rsp"))
                os.chdir(Path(work_path))
                rsp_patch()
                os.chdir(Path(work_path + "/../"))
                os.system(Path(work_path + "/../artifacts/Stevedore/android-ndk-mac/toolchains/llvm/prebuilt/darwin-x86_64/bin/clang++") + " @" + Path(work_path + "/../artifacts/rsp/14590475716575637239.rsp"))
                os.system(Path(flutter_root_path + "/buildtools/mac-x64/clang/bin/clang++") + " @" + Path(work_path + "/../artifacts/rsp/14590475716575637239.rsp"))
                copy_file(Path(work_path + "/../artifacts/libUIWidgets/release_Android_arm32/libUIWidgets.so"), Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/Android/libUIWidgets.so"))
            elif platform == "ios":
                print("\nStarting prlink library...")
                os.chdir(Path(work_path + "/../"))
                tundra_file=Path(work_path + "/../artifacts/tundra.dag.json")
                prelinkfiles(tundra_file, runtime_mode, output_path, work_path, bitcode)
    elif runtime_mode == "debug":
        os.system("rm -rf build_debug/*")
        if platform == "windows":
            os.system("bee.exe win_debug")
        else:
            os.system("mono bee.exe " + platform +"_debug")
        copy_file(Path(work_path + "/../build_debug/"), Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/" + dest_folder))

def revert_patches():
    global flutter_root_path
    print("\nRevert patches...")

    os.chdir(Path(flutter_root_path + "/flutter/third_party/txt"))
    os.system("patch -R < BUILD.gn.patch")

    if platform == "ios" or platform == "mac":
        os.chdir(Path(flutter_root_path + "/build/mac"))
        os.system("patch -R < find_sdk.patch")
        if platform == "ios":
            os.chdir(Path(work_path))
            if os.path.exists(Path(work_path + "/bitcode.conf")):
                os.remove("bitcode.conf")
    elif platform == "android":
        os.chdir(Path(flutter_root_path + "/build/secondary/third_party/libcxxabi"))
        os.system("patch -R < BUILD_2.gn.patch")
    elif platform == "windows":
        os.chdir(Path(flutter_root_path + "/third_party/angle"))
        os.system("patch -R < BUILD.gn.patch")

        os.chdir(Path(flutter_root_path + "/third_party/angle/src/libANGLE/renderer/d3d/d3d11/"))
        os.system("patch -R < cpp.patch")

        os.chdir(Path(flutter_root_path + "/third_party/skia/"))
        os.system("patch -R < BUILD_2.gn.patch")

def copy_file(source_path, target_path):
    if not os.path.exists(target_path):
        os.makedirs(target_path)
    if os.path.exists(source_path):
        if os.path.isfile(source_path):
            shutil.copy(source_path, target_path)
        elif os.path.isdir(source_path):
            for root, dirs, files in os.walk(source_path):
                for file in files:
                    src_file = os.path.join(root, file)
                    shutil.copy(src_file, target_path)
    

def rsp_patch():
    global work_path
    file_data = ""
    file = Path(work_path + "../artifacts/rsp/14590475716575637239.rsp")
    old_str = ',--icf-iterations=5'
    with open(file, "r") as f:
        for line in f:
            if old_str in line:
                line = line.replace(old_str,'')
            file_data += line
    with open(file,"w") as f:
        f.write(file_data)
    return "rsp modified"

def get_xcode_path():
    res = os.popen('xcode-select -p')
    return res.read()

def get_target_files(tundra_file, runtime_mode):
    if not os.path.exists(tundra_file):
        print('tundra.dag.json file not found')
        return None
    with open(tundra_file, 'r') as f:
        temp = json.loads(f.read())
        json_list = temp['Nodes']
        target_files=''
        for item in json_list:
            if item['Annotation'].startswith('Lib_iOS_arm64') and item['Annotation'].find(runtime_mode) != -1:
                action = item['Action']
                o_file_list = action.split("\"")
                for o in o_file_list:
                    if o.endswith('.o'):
                        target_files += ' '+o
        return target_files
    
def prelinkfiles(tundra_file, runtime_mode, output_path, work_path, bitcode):
    global flutter_root_path
    target_files = get_target_files(tundra_file, runtime_mode)
    if not target_files:
        print("get prelink xxx.o files failed")
    else:
        os.system('nm -j ' + Path(flutter_root_path + '/out/' + output_path + '/obj/flutter/third_party/txt/libtxt_lib.a') +' > third.symbol')
        xcode_path = get_xcode_path().strip()
        os.system('\"' + Path(xcode_path + '/Toolchains/XcodeDefault.xctoolchain/usr/bin/ld')+ '\" -r -arch arm64 ' + bitcode + ' -syslibroot ' + Path(xcode_path + '/Platforms/iPhoneOS.platform/Developer/SDKs/iPhoneOS.sdk') + ' -unexported_symbols_list third.symbol ' + target_files + ' ' + Path(flutter_root_path + '/out/' + output_path + '/obj/flutter/third_party/txt/libtxt_lib.a') + ' -o "libUIWidgets.o"')
        os.system('\"' + Path(xcode_path + '/Toolchains/XcodeDefault.xctoolchain/usr/bin/libtool') + '\" -arch_only arm64 -static "libUIWidgets.o" -o "libUIWidgets.a"')
        os.system('\"' + Path(xcode_path + '/Toolchains/XcodeDefault.xctoolchain/usr/bin/strip') + '\" -x "libUIWidgets.a"')
        os.system('cp -r libUIWidgets.a ' + '../com.unity.uiwidgets/Runtime/Plugins/ios/libUIWidgets.a')
        copy_file(Path(work_path + "/../libUIWidgets.a"), Path(work_path + "/../../com.unity.uiwidgets/Runtime/Plugins/ios/libUIWidgets.a"))
def main():
    get_opts()
    engine_path_check()
    bitcode_conf()
    set_params()
    set_env_verb()   
    get_depot_tools()
    get_flutter_engine()
    compile_engine()
    build_engine()
    revert_patches()

if __name__=="__main__":
    main()