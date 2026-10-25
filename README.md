# Problem statement
Whenever I'm watching a movie with my dad, I always find myself taking the time to sync the movie with the Persian subtitles. There should be a way to automate this process.

# Methodology
Normally, we can safely assume that you just need to shift the subtitles by a certain amount of time to sync them with the movie. 

The SRT file already tells you the duration, order, and timestamps of each subtitle cue.

If you run a speech activity detection algorithm on the movie, you can also get the duration, order, and timestamps of each speech segment. 

So for this, we want to find the best shift value so that the subtitles and speech segments align as closely as possible.

## Benefits of this approach
The approach doesn't require any knowledge of the language of the subtitles or probably even the language of the movie. 

## VAD credits

Speech activity detection is powered by the official [Silero VAD](https://github.com/snakers4/silero-vad)
ONNX model, integrated through [sherpa-onnx](https://github.com/k2-fsa/sherpa-onnx).

# How to use this project

See the [LLM wiki index](llm-wiki/index.md) for the project guide and shared
technical context. To run subtitle synchronization, start with the [CLI usage
guide](llm-wiki/running-cli.md).

