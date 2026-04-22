using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

namespace Synthic.UI
{
    public class SequencerUIController : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private Sequencer sequencer;
        [SerializeField] private PolyphonicGenerator generator;

        private VisualElement _root;

        // step grid
        private List<Button> _stepButtons = new();
        private int _selectedStep = 0;

        // pattern buttons
        private List<Button> _patternButtons = new();

        // transport
        private Button _btnPlay;
        private Button _btnStop;
        private Button _btnPause;
        private FloatField _fieldBpm;
        private Slider _sliderSwing;
        private Label _labelSwingValue;

        // step editor
        private DropdownField _dropdownNote;
        private IntegerField _fieldOctave;
        private Slider _sliderVelocity;
        private Label _labelVelocityValue;
        private Slider _sliderGate;
        private Label _labelGateValue;
        private Button _btnTypeNormal;
        private Button _btnTypeRest;
        private Button _btnTypeTie;
        private Button _btnTypeAccent;

        // waveform
        private Button _btnWaveSine;
        private Button _btnWaveSaw;
        private Button _btnWaveSquare;
        private Button _btnWaveTriangle;

        // adsr
        private Slider _sliderAttack;
        private Slider _sliderDecay;
        private Slider _sliderSustain;
        private Slider _sliderRelease;
        private Label _labelAttackValue;
        private Label _labelDecayValue;
        private Label _labelSustainValue;
        private Label _labelReleaseValue;

        private static readonly string[] NoteNames =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private void OnEnable()
        {
            _root = uiDocument.rootVisualElement;
            BindTransport();
            BindStepGrid();
            BindStepEditor();
            BindWaveform();
            BindADSR();
            BindPatternSelector();
            RefreshAll();
        }

        private void Update()
        {
            // highlight the currently playing step
            if (sequencer == null) return;
            for (int i = 0; i < _stepButtons.Count; i++)
            {
                _stepButtons[i].RemoveFromClassList("playing");
                if (sequencer.Playing && sequencer.CurrentStep == i)
                    _stepButtons[i].AddToClassList("playing");
            }
        }

        // ── Transport ──────────────────────────────────────────────

        private void BindTransport()
        {
            _btnPlay  = _root.Q<Button>("btn-play");
            _btnStop  = _root.Q<Button>("btn-stop");
            _btnPause = _root.Q<Button>("btn-pause");
            _fieldBpm = _root.Q<FloatField>("field-bpm");
            _sliderSwing     = _root.Q<Slider>("slider-swing");
            _labelSwingValue = _root.Q<Label>("label-swing-value");

            _btnPlay.clicked  += () => sequencer.Play();
            _btnStop.clicked  += () => sequencer.Stop();
            _btnPause.clicked += () =>
            {
                if (sequencer.Playing) sequencer.Pause();
                else sequencer.Resume();
            };

            _fieldBpm.RegisterValueChangedCallback(evt =>
            {
                sequencer.BPM = evt.newValue;
            });

            _sliderSwing.RegisterValueChangedCallback(evt =>
            {
                _labelSwingValue.text = evt.newValue.ToString("F2");
                if (sequencer != null && CurrentPattern != null)
                    CurrentPattern.swing = evt.newValue;
            });
        }

        // ── Pattern Selector ───────────────────────────────────────

        private void BindPatternSelector()
        {
            var patternRow = _root.Q<VisualElement>("pattern-row");
            var btnAdd     = _root.Q<Button>("btn-add-pattern");

            btnAdd.clicked += () =>
            {
                sequencer.AddPattern();
                RefreshPatternButtons();
            };

            RefreshPatternButtons();
        }

private void RefreshPatternButtons()
{
    var patternRow = _root.Q<VisualElement>("pattern-row");
    var btnAdd     = _root.Q<Button>("btn-add-pattern");

    // only remove buttons that are actually children of patternRow
    foreach (var btn in _patternButtons)
    {
        if (btn.parent == patternRow)
            patternRow.Remove(btn);
    }
    _patternButtons.Clear();

    for (int i = 0; i < sequencer.PatternCount; i++)
    {
        int index = i;
        var btn = new Button(() =>
        {
            sequencer.JumpToPattern(index);
            RefreshPatternButtons();
            RefreshStepGrid();
            RefreshStepEditor();
        });
        btn.text = (i + 1).ToString();
        btn.AddToClassList("pattern-button");
        if (i == sequencer.CurrentPattern)
            btn.AddToClassList("active");

        _patternButtons.Add(btn);
        patternRow.Insert(patternRow.childCount - 1, btn);
    }
}
        // ── Step Grid ──────────────────────────────────────────────

        private void BindStepGrid()
        {
            var grid = _root.Q<VisualElement>("step-grid");
            grid.Clear();
            _stepButtons.Clear();

            for (int i = 0; i < 16; i++)
            {
                int index = i;
                var btn = new Button(() => OnStepClicked(index));
                btn.AddToClassList("step-button");
                grid.Add(btn);
                _stepButtons.Add(btn);
            }

            RefreshStepGrid();
        }

        private void OnStepClicked(int index)
        {
            if (_selectedStep == index)
            {
                // toggle active on double-click same step
                if (CurrentPattern != null && index < CurrentPattern.steps.Count)
                    CurrentPattern.steps[index].active = !CurrentPattern.steps[index].active;
            }

            _selectedStep = index;
            RefreshStepGrid();
            RefreshStepEditor();
        }

        private void RefreshStepGrid()
        {
            if (CurrentPattern == null) return;

            for (int i = 0; i < _stepButtons.Count; i++)
            {
                var btn  = _stepButtons[i];
                var step = i < CurrentPattern.steps.Count ? CurrentPattern.steps[i] : null;

                btn.RemoveFromClassList("on");
                btn.RemoveFromClassList("rest");
                btn.RemoveFromClassList("tie");
                btn.RemoveFromClassList("accent");
                btn.RemoveFromClassList("selected");

                if (step != null)
                {
                    if (step.active)
                    {
                        switch (step.stepType)
                        {
                            case SequenceStep.StepType.Normal:  btn.AddToClassList("on");     break;
                            case SequenceStep.StepType.Rest:    btn.AddToClassList("rest");   break;
                            case SequenceStep.StepType.Tie:     btn.AddToClassList("tie");    break;
                            case SequenceStep.StepType.Accent:  btn.AddToClassList("accent"); break;
                        }
                    }

                    // show note name on button
                    btn.text = step.active
                        ? $"{NoteNames[(int)step.note]}{step.octave}"
                        : "-";
                }

                if (i == _selectedStep)
                    btn.AddToClassList("selected");
            }
        }

        // ── Step Editor ────────────────────────────────────────────

        private void BindStepEditor()
        {
            _dropdownNote       = _root.Q<DropdownField>("dropdown-note");
            _fieldOctave        = _root.Q<IntegerField>("field-octave");
            _sliderVelocity     = _root.Q<Slider>("slider-velocity");
            _labelVelocityValue = _root.Q<Label>("label-velocity-value");
            _sliderGate         = _root.Q<Slider>("slider-gate");
            _labelGateValue     = _root.Q<Label>("label-gate-value");
            _btnTypeNormal      = _root.Q<Button>("btn-type-normal");
            _btnTypeRest        = _root.Q<Button>("btn-type-rest");
            _btnTypeTie         = _root.Q<Button>("btn-type-tie");
            _btnTypeAccent      = _root.Q<Button>("btn-type-accent");

            // populate note dropdown
            _dropdownNote.choices = new List<string>(NoteNames);

            _dropdownNote.RegisterValueChangedCallback(evt =>
            {
                var step = SelectedStep;
                if (step == null) return;
                step.note = (Note.Name)Array.IndexOf(NoteNames, evt.newValue);
                RefreshStepGrid();
            });

            _fieldOctave.RegisterValueChangedCallback(evt =>
            {
                var step = SelectedStep;
                if (step == null) return;
                step.octave = Mathf.Clamp(evt.newValue, 0, 8);
                RefreshStepGrid();
            });

            _sliderVelocity.RegisterValueChangedCallback(evt =>
            {
                _labelVelocityValue.text = evt.newValue.ToString("F2");
                if (SelectedStep != null) SelectedStep.velocity = evt.newValue;
            });

            _sliderGate.RegisterValueChangedCallback(evt =>
            {
                _labelGateValue.text = evt.newValue.ToString("F2");
                if (SelectedStep != null) SelectedStep.gateLength = evt.newValue;
            });

            _btnTypeNormal.clicked += () => SetStepType(SequenceStep.StepType.Normal);
            _btnTypeRest.clicked   += () => SetStepType(SequenceStep.StepType.Rest);
            _btnTypeTie.clicked    += () => SetStepType(SequenceStep.StepType.Tie);
            _btnTypeAccent.clicked += () => SetStepType(SequenceStep.StepType.Accent);

            RefreshStepEditor();
        }

        private void SetStepType(SequenceStep.StepType type)
        {
            if (SelectedStep == null) return;
            SelectedStep.stepType = type;
            RefreshStepGrid();
            RefreshStepEditor();
        }

        private void RefreshStepEditor()
        {
            var step = SelectedStep;
            if (step == null) return;

            _dropdownNote.SetValueWithoutNotify(NoteNames[(int)step.note]);
            _fieldOctave.SetValueWithoutNotify(step.octave);
            _sliderVelocity.SetValueWithoutNotify(step.velocity);
            _labelVelocityValue.text = step.velocity.ToString("F2");
            _sliderGate.SetValueWithoutNotify(step.gateLength);
            _labelGateValue.text = step.gateLength.ToString("F2");

            // step type buttons
            _btnTypeNormal.RemoveFromClassList("active");
            _btnTypeRest.RemoveFromClassList("active");
            _btnTypeTie.RemoveFromClassList("active");
            _btnTypeAccent.RemoveFromClassList("active");

            switch (step.stepType)
            {
                case SequenceStep.StepType.Normal: _btnTypeNormal.AddToClassList("active"); break;
                case SequenceStep.StepType.Rest:   _btnTypeRest.AddToClassList("active");   break;
                case SequenceStep.StepType.Tie:    _btnTypeTie.AddToClassList("active");    break;
                case SequenceStep.StepType.Accent: _btnTypeAccent.AddToClassList("active"); break;
            }
        }

        // ── Waveform ───────────────────────────────────────────────

        private void BindWaveform()
        {
            _btnWaveSine     = _root.Q<Button>("btn-wave-sine");
            _btnWaveSaw      = _root.Q<Button>("btn-wave-saw");
            _btnWaveSquare   = _root.Q<Button>("btn-wave-square");
            _btnWaveTriangle = _root.Q<Button>("btn-wave-triangle");

            _btnWaveSine.clicked     += () => SetWaveform(PolyphonicGenerator.WaveformType.Sine);
            _btnWaveSaw.clicked      += () => SetWaveform(PolyphonicGenerator.WaveformType.Saw);
            _btnWaveSquare.clicked   += () => SetWaveform(PolyphonicGenerator.WaveformType.Square);
            _btnWaveTriangle.clicked += () => SetWaveform(PolyphonicGenerator.WaveformType.Triangle);

            RefreshWaveform();
        }

        private void SetWaveform(PolyphonicGenerator.WaveformType type)
        {
            if (generator == null) return;
            generator.Waveform = type;
            RefreshWaveform();
        }

        private void RefreshWaveform()
        {
            if (generator == null) return;
            _btnWaveSine.RemoveFromClassList("active");
            _btnWaveSaw.RemoveFromClassList("active");
            _btnWaveSquare.RemoveFromClassList("active");
            _btnWaveTriangle.RemoveFromClassList("active");

            switch (generator.Waveform)
            {
                case PolyphonicGenerator.WaveformType.Sine:
                    _btnWaveSine.AddToClassList("active"); break;
                case PolyphonicGenerator.WaveformType.Saw:
                    _btnWaveSaw.AddToClassList("active"); break;
                case PolyphonicGenerator.WaveformType.Square:
                    _btnWaveSquare.AddToClassList("active"); break;
                case PolyphonicGenerator.WaveformType.Triangle:
                    _btnWaveTriangle.AddToClassList("active"); break;
            }
        }

        // ── ADSR ───────────────────────────────────────────────────

        private void BindADSR()
        {
            _sliderAttack  = _root.Q<Slider>("slider-attack");
            _sliderDecay   = _root.Q<Slider>("slider-decay");
            _sliderSustain = _root.Q<Slider>("slider-sustain");
            _sliderRelease = _root.Q<Slider>("slider-release");
            _labelAttackValue  = _root.Q<Label>("label-attack-value");
            _labelDecayValue   = _root.Q<Label>("label-decay-value");
            _labelSustainValue = _root.Q<Label>("label-sustain-value");
            _labelReleaseValue = _root.Q<Label>("label-release-value");

            _sliderAttack.RegisterValueChangedCallback(evt =>
            {
                _labelAttackValue.text = evt.newValue.ToString("F2");
                if (generator != null) generator.SetAttack(evt.newValue);
            });

            _sliderDecay.RegisterValueChangedCallback(evt =>
            {
                _labelDecayValue.text = evt.newValue.ToString("F2");
                if (generator != null) generator.SetDecay(evt.newValue);
            });

            _sliderSustain.RegisterValueChangedCallback(evt =>
            {
                _labelSustainValue.text = evt.newValue.ToString("F2");
                if (generator != null) generator.SetSustain(evt.newValue);
            });

            _sliderRelease.RegisterValueChangedCallback(evt =>
            {
                _labelReleaseValue.text = evt.newValue.ToString("F2");
                if (generator != null) generator.SetRelease(evt.newValue);
            });

            RefreshADSR();
        }

        private void RefreshADSR()
        {
            if (generator == null) return;
            _sliderAttack.SetValueWithoutNotify(generator.Attack);
            _sliderDecay.SetValueWithoutNotify(generator.Decay);
            _sliderSustain.SetValueWithoutNotify(generator.Sustain);
            _sliderRelease.SetValueWithoutNotify(generator.Release);
            _labelAttackValue.text  = generator.Attack.ToString("F2");
            _labelDecayValue.text   = generator.Decay.ToString("F2");
            _labelSustainValue.text = generator.Sustain.ToString("F2");
            _labelReleaseValue.text = generator.Release.ToString("F2");
        }

        // ── Helpers ────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshPatternButtons();
            RefreshStepGrid();
            RefreshStepEditor();
            RefreshWaveform();
            RefreshADSR();

            if (sequencer != null)
                _fieldBpm.SetValueWithoutNotify(sequencer.BPM);
        }

        private Sequence CurrentPattern =>
            sequencer != null && sequencer.CurrentPattern < sequencer.PatternCount
                ? sequencer.GetPattern(sequencer.CurrentPattern)
                : null;

        private SequenceStep SelectedStep =>
            CurrentPattern != null && _selectedStep < CurrentPattern.steps.Count
                ? CurrentPattern.steps[_selectedStep]
                : null;
    }
}