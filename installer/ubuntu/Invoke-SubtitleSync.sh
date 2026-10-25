#!/usr/bin/env bash
set -euo pipefail

show_error() { zenity --no-markup --error --title='Subtitle Syncer' --text="$1" >/dev/null 2>&1 || true; }
show_info() { zenity --no-markup --info --title='Subtitle Syncer' --text="$1" >/dev/null 2>&1 || true; }

subtitle_path="${NAUTILUS_SCRIPT_SELECTED_FILE_PATHS:-}"
subtitle_path="${subtitle_path%%$'\n'*}"
if [[ -z "$subtitle_path" || ! -f "$subtitle_path" ]]; then
    show_error 'Select one existing SRT file.'
    exit 1
fi
if [[ "${subtitle_path##*.}" != 'srt' && "${subtitle_path##*.}" != 'SRT' ]]; then
    show_error 'The selected file must have an .srt extension.'
    exit 1
fi

video_path="$(zenity --file-selection --title='Choose the movie for this subtitle' \
    --file-filter='Video files | *.mkv *.mp4 *.avi *.mov *.webm *.mpeg *.mpg *.wmv' \
    --file-filter='All files | *' 2>/dev/null)" || exit 0
[[ -n "$video_path" && -f "$video_path" ]] || exit 0

result_file="$(mktemp "${TMPDIR:-/tmp}/subtitle-syncer-result.XXXXXX")"
progress_pid=''
cleanup_command_files() {
    if [[ -n "$progress_pid" ]] && kill -0 "$progress_pid" 2>/dev/null; then
        kill "$progress_pid" 2>/dev/null || true
        wait "$progress_pid" 2>/dev/null || true
    fi
    rm -f "$result_file"
}
trap cleanup_command_files EXIT

zenity --progress --pulsate --no-cancel --title='Subtitle Syncer' \
    --text='Analyzing the movie and synchronizing subtitles…' \
    >/dev/null 2>&1 &
progress_pid=$!
set +e
subtitle-syncer --video "$video_path" --subtitles "$subtitle_path" --device auto >"$result_file" 2>&1
command_status=$?
set -e
if [[ -n "$progress_pid" ]] && kill -0 "$progress_pid" 2>/dev/null; then
    kill "$progress_pid" 2>/dev/null || true
    wait "$progress_pid" 2>/dev/null || true
fi
progress_pid=''
result="$(<"$result_file")"
if [[ "$command_status" -ne 0 ]]; then
    if [[ -z "$result" ]]; then
        result="The synchronizer exited with status $command_status and returned no diagnostic."
    fi
    show_error "$(printf 'Synchronization could not finish.\n\n%s' "$result")"
    exit 1
fi
offset="$(printf '%s\n' "$result" | perl -ne 'print "$1\n" and exit if /"offset_seconds"\s*:\s*(-?(?:\d+(?:\.\d*)?|\.\d+)(?:[eE][+-]?\d+)?)/')"
if [[ -z "$offset" ]]; then
    show_error 'Synchronization did not return a usable offset.'
    exit 1
fi

directory="$(dirname "$subtitle_path")"
filename="$(basename "$subtitle_path")"
stem="${filename%.*}"
extension=".${filename##*.}"
corrected_path="$directory/$stem-synced$extension"
index=2
while [[ -e "$corrected_path" ]]; do
    corrected_path="$directory/$stem-synced-$index$extension"
    ((++index))
done

temporary_path="$(mktemp "$directory/.$stem.synced.XXXXXX")"
rewrite_error_file="$(mktemp "${TMPDIR:-/tmp}/subtitle-syncer-rewrite-error.XXXXXX")"
trap 'rm -f "$temporary_path" "$rewrite_error_file"' EXIT
if ! perl -CSDA - "$subtitle_path" "$offset" > "$temporary_path" 2>"$rewrite_error_file" <<'PERL'
use strict;
use warnings;
use Encode qw(decode encode FB_CROAK);
my ($path, $offset) = @ARGV;
open my $input, '<:raw', $path or die "Could not read subtitle file: $!\n";
local $/;
my $bytes = <$input>;
my ($encoding, $prefix) = ('UTF-8', '');
if (substr($bytes, 0, 2) eq "\xFF\xFE") {
    ($encoding, $prefix) = ('UTF-16LE', "\xFF\xFE");
    $bytes = substr($bytes, 2);
} elsif (substr($bytes, 0, 2) eq "\xFE\xFF") {
    ($encoding, $prefix) = ('UTF-16BE', "\xFE\xFF");
    $bytes = substr($bytes, 2);
} elsif (substr($bytes, 0, 3) eq "\xEF\xBB\xBF") {
    $prefix = "\xEF\xBB\xBF";
    $bytes = substr($bytes, 3);
}
my $text = decode($encoding, $bytes, FB_CROAK);
my $count = 0;
sub seconds { my ($v) = @_; $v =~ /^(\d+):(\d{2}):(\d{2})[,.](\d{3})$/ or die "Invalid SRT timestamp: $v\n"; return $1 * 3600 + $2 * 60 + $3 + $4 / 1000; }
sub timestamp {
    my ($v) = @_;
    my $ms = int($v * 1000 + ($v >= 0 ? 0.5 : -0.5));
    die "The estimated correction would create a timestamp before 00:00:00,000.\n" if $ms < 0;
    my $h = int($ms / 3_600_000); $ms %= 3_600_000;
    my $m = int($ms / 60_000); $ms %= 60_000;
    my $s = int($ms / 1000); $ms %= 1000;
    return sprintf('%02d:%02d:%02d,%03d', $h, $m, $s, $ms);
}
$text =~ s{^(\d{2,}:\d{2}:\d{2}[,.]\d{3})(\s+-->\s+)(\d{2,}:\d{2}:\d{2}[,.]\d{3})([^\r\n]*)(\r?)$}{
    my ($start, $arrow, $end, $suffix, $line_ending) = ($1, $2, $3, $4, $5);
    my $new_start = seconds($start) + $offset;
    my $new_end = seconds($end) + $offset;
    die "The estimated correction would create an invalid subtitle interval.\n" if $new_end <= $new_start;
    ++$count;
    timestamp($new_start) . $arrow . timestamp($new_end) . $suffix . $line_ending;
}gme;
die "No valid SRT timestamps were found.\n" unless $count;
binmode STDOUT, ':raw';
print $prefix, encode($encoding, $text, FB_CROAK);
PERL
then
    rewrite_error="$(<"$rewrite_error_file")"
    if [[ -z "$rewrite_error" ]]; then
        rewrite_error='The subtitle rewrite returned no diagnostic.'
    fi
    show_error "$(printf 'Synchronization could not create a corrected subtitle file.\n\n%s' "$rewrite_error")"
    exit 1
fi
rm -f "$rewrite_error_file"
if ! mv "$temporary_path" "$corrected_path"; then
    show_error 'Synchronization could not save the corrected subtitle file.'
    exit 1
fi
trap - EXIT
show_info "$(printf 'Created a corrected subtitle file:\n%s' "$corrected_path")"
