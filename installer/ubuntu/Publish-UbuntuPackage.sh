#!/usr/bin/env bash
set -euo pipefail

usage() {
    cat <<'USAGE'
Usage: Publish-UbuntuPackage.sh --output-dir <path> [--model-dir <path>] [--version <version>]

Builds an amd64 Debian package for Ubuntu. The package contains a
self-contained linux-x64 .NET release and declares ffmpeg as an apt dependency;
FFmpeg is therefore installed and updated by Ubuntu rather than bundled here.
USAGE
}

output_directory=''
model_directory=''
package_version='1.0.0'
script_directory="$(cd "$(dirname "$0")" && pwd)"
repository_root="$(cd "$script_directory/../.." && pwd)"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --output-dir) output_directory="${2:-}"; shift 2 ;;
        --model-dir) model_directory="${2:-}"; shift 2 ;;
        --version) package_version="${2:-}"; shift 2 ;;
        --help|-h) usage; exit 0 ;;
        *) usage >&2; exit 64 ;;
    esac
done

if [[ -z "$output_directory" ]]; then
    usage >&2
    exit 64
fi
if [[ -z "$model_directory" ]]; then
    model_directory="$repository_root/artifacts/duration_alignment/model_60m_120s"
fi
vad_model_path="$repository_root/src/SubtitleSyncer.Vad/models/silero_vad.onnx"
if [[ ! "$package_version" =~ ^[0-9][0-9A-Za-z.+~-]*$ ]]; then
    printf 'Invalid Debian package version: %s\n' "$package_version" >&2
    exit 64
fi
if [[ ! -f "$model_directory/duration_offset_model.onnx" || ! -f "$model_directory/duration_offset_model.json" ]]; then
    printf 'The model directory must contain duration_offset_model.onnx and duration_offset_model.json.\n' >&2
    exit 1
fi
if [[ ! -f "$vad_model_path" ]]; then
    printf 'The bundled Silero VAD model was not found: %s\n' "$vad_model_path" >&2
    exit 1
fi
if ! command -v dotnet >/dev/null; then
    printf 'The release machine needs the .NET SDK to publish the self-contained executable. End users do not.\n' >&2
    exit 1
fi
nuget_root="$(dotnet nuget locals global-packages --list | sed -n 's/^global-packages: //p')"
sherpa_native_directory="$nuget_root/org.k2fsa.sherpa.onnx.runtime.linux-x64/2.0.0/runtimes/linux-x64/native"
if [[ ! -f "$sherpa_native_directory/libonnxruntime.so" || ! -f "$sherpa_native_directory/libsherpa-onnx-c-api.so" ]]; then
    printf 'The sherpa-onnx Linux native runtime was not found under %s\n' "$sherpa_native_directory" >&2
    exit 1
fi
if ! command -v dpkg-deb >/dev/null; then
    printf 'dpkg-deb is required to build the Ubuntu package.\n' >&2
    exit 1
fi

mkdir -p "$output_directory"
archive="$output_directory/subtitle-syncer_${package_version}_amd64.deb"
if [[ -e "$archive" ]]; then
    printf 'Refusing to overwrite existing package: %s\n' "$archive" >&2
    exit 1
fi

staging_directory="$(mktemp -d "${TMPDIR:-/tmp}/subtitle-syncer-deb.XXXXXX")"
package_directory="$staging_directory/package"
install_directory="$package_directory/usr/lib/subtitle-syncer"
temporary_archive="$archive.tmp.$$"
cleanup() {
    rm -rf "$staging_directory"
    if [[ -e "$temporary_archive" ]]; then
        rm -f "$temporary_archive"
    fi
}
trap cleanup EXIT

mkdir -p "$package_directory/DEBIAN" "$install_directory" "$package_directory/usr/bin" "$package_directory/usr/share/doc/subtitle-syncer"

dotnet publish "$repository_root/apps/SubtitleSyncer.Sync.Console/SubtitleSyncer.Sync.Console.csproj" \
    --configuration Release --runtime linux-x64 --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false \
    -p:EnableCompressionInSingleFile=true \
    --output "$install_directory"

find "$install_directory" -mindepth 1 -type f ! -name 'SubtitleSyncer.Sync.Console' ! -name '*.so' -delete
find "$install_directory" -depth -type d -empty -delete
mv "$install_directory/SubtitleSyncer.Sync.Console" "$install_directory/subtitle-syncer-core"
cp "$sherpa_native_directory/libonnxruntime.so" "$sherpa_native_directory/libsherpa-onnx-c-api.so" "$install_directory/"
cp "$model_directory/duration_offset_model.onnx" "$model_directory/duration_offset_model.json" "$install_directory/"
mkdir -p "$install_directory/models"
cp "$vad_model_path" "$install_directory/models/silero_vad.onnx"
cp "$repository_root/src/SubtitleSyncer.Vad/licenses/silero-vad/LICENSE" "$package_directory/usr/share/doc/subtitle-syncer/LICENSE-silero-vad"
mkdir -p "$package_directory/usr/share/nautilus/scripts"
cp "$script_directory/Invoke-SubtitleSync.sh" "$package_directory/usr/share/nautilus/scripts/Sync with movie using Subtitle Syncer"
cp "$script_directory/postinst" "$package_directory/DEBIAN/postinst"

cat > "$package_directory/usr/bin/subtitle-syncer" <<'WRAPPER'
#!/usr/bin/env bash
set -euo pipefail
exec /usr/lib/subtitle-syncer/subtitle-syncer-core \
    --model /usr/lib/subtitle-syncer/duration_offset_model.onnx "$@"
WRAPPER
chmod 0755 "$package_directory/usr/bin/subtitle-syncer" "$install_directory/subtitle-syncer-core"
chmod 0755 "$package_directory/usr/share/nautilus/scripts/Sync with movie using Subtitle Syncer"
chmod 0755 "$package_directory/DEBIAN/postinst"

cat > "$package_directory/DEBIAN/control" <<CONTROL
Package: subtitle-syncer
Version: $package_version
Section: sound
Priority: optional
Architecture: amd64
Maintainer: Subtitle Syncer contributors
Depends: ffmpeg, zenity
Description: Local constant-offset subtitle synchronizer
 Subtitle Syncer estimates a constant subtitle timing correction from a movie
 and an SRT file using local speech activity detection.
CONTROL

dpkg-deb --build --root-owner-group "$package_directory" "$temporary_archive" >/dev/null
mv "$temporary_archive" "$archive"
printf 'Created %s\n' "$archive"
