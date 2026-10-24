const form = document.querySelector('#source-form');
const status = document.querySelector('#status');
const review = document.querySelector('#review');
const result = document.querySelector('#result');
const confirm = document.querySelector('#confirm');
let selectedCue = null;
let selectedSpeech = null;

fetch('/api/defaults').then(response => response.json()).then(defaults => {
  document.querySelector('#movie').value = defaults.moviePath;
  document.querySelector('#subtitle').value = defaults.subtitlePath;
  document.querySelector('#seconds').value = defaults.analysisSeconds;
}).catch(() => {});

function item(item, kind, onSelect) {
  const button = document.createElement('button');
  button.type = 'button';
  button.className = `item ${kind}`;
  button.innerHTML = `<span class="time">${item.start} — ${item.end}</span><span class="text"></span>${kind === 'cue' ? `<span class="translation"></span>` : `<span class="confidence">ASR confidence: ${Math.round(item.confidence * 100)}%</span>`}`;
  button.querySelector('.text').textContent = kind === 'cue' ? item.persianText : item.text;
  if (kind === 'cue') button.querySelector('.translation').textContent = item.englishTranslation;
  button.addEventListener('click', () => onSelect(item, button));
  return button;
}

function choose(kind, value, button) {
  document.querySelectorAll(`.item.${kind}`).forEach(item => item.classList.remove('selected'));
  button.classList.add('selected');
  if (kind === 'cue') {
    selectedCue = value;
    document.querySelector('#left-selection').textContent = `Persian ${value.start}: ${value.persianText.replaceAll('\n', ' / ')}`;
  } else {
    selectedSpeech = value;
    document.querySelector('#right-selection').textContent = `English ${value.start}: ${value.text}`;
  }
  confirm.disabled = !(selectedCue && selectedSpeech);
  result.hidden = true;
}

form.addEventListener('submit', async event => {
  event.preventDefault();
  selectedCue = selectedSpeech = null;
  confirm.disabled = true;
  review.hidden = true;
  result.hidden = true;
  status.textContent = 'Preparing audio, transcribing speech, and translating subtitles…';
  document.querySelector('#load').disabled = true;
  try {
    const response = await fetch('/api/review', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({ moviePath: document.querySelector('#movie').value, subtitlePath: document.querySelector('#subtitle').value, analysisSeconds: Number(document.querySelector('#seconds').value) }) });
    const data = await response.json();
    if (!response.ok) throw new Error(data);
    const cues = document.querySelector('#cues');
    const speech = document.querySelector('#speech');
    cues.replaceChildren(...data.cues.map(value => item(value, 'cue', (choice, button) => choose('cue', choice, button))));
    speech.replaceChildren(...data.speech.map(value => item(value, 'speech', (choice, button) => choose('speech', choice, button))));
    review.hidden = false;
    status.textContent = `Ready: ${data.cues.length} subtitles and ${data.speech.length} transcription segments.`;
  } catch (error) {
    status.textContent = `Unable to prepare review: ${error.message}`;
  } finally {
    document.querySelector('#load').disabled = false;
  }
});

confirm.addEventListener('click', async () => {
  const response = await fetch('/api/offset', { method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify({cueIndex: selectedCue.index, speechIndex: selectedSpeech.index}) });
  const data = await response.json();
  if (!response.ok) { status.textContent = data; return; }
  result.hidden = false;
  result.innerHTML = `<p class="eyebrow">CONFIRMED OFFSET</p><strong>${data.offset}</strong><p>Delay the subtitle timeline by this amount. The pair was selected manually; no fuzzy-text score was used.</p>`;
});
