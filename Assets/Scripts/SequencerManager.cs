using System.Collections;
using System.Collections.Generic;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;


/// <summary>
/// Sequencer Manager is responsible for controlling all the sequencer tracks, generating random 
/// music during the game loop
/// 
/// SEQUENCERS 
/// There are 9 sequencers:
/// 4 percussive (kick, snare, hihat, perc)
/// 3 melodic (bass, synth, pad)
/// 1 lyric (calls out the object names)
/// 1 empty one discussed below
/// These sequencers are child objects of the manager
/// They are all driven by a sequencer driver so they're all running at on the same clock at the same tempo
/// 
/// EMPTY SEQUENCER
/// There is also an 'empty' sequencer that is being used to count bars 
/// It is not actually empty it has a clip assigned otherwise it throws errors
/// This track should have the 'is muted' flag on the sequencer set to false otherwise it won't run
/// And the initial audio source vol set to 0 so it won't be audible
/// This is done in the code so don't worry about it 
/// 
/// HOW IT WORKS
/// At the start it just generates a random pattern for the kick sequencer 
/// When an object lands, it goes through all 7 (percussive and melodic) sequencers and 
/// randomly generates patterns for each of them, then randomlly decides which ones should be audible
/// and mutes the others
/// it also randomly assigns sounds to each of them, from the pool of sounds attached to each sequencer
/// it also assigns the correct object name voiceover and plays that ever four bars
/// each bar, for variation, it mutes or unmutes one of the sequencers, and changes the pattern of one of them 
/// this way there is gradual variation but still feeling like it's part of the same 'song'
/// then it totally reshuffles everything when a new object lands
/// 
/// 
/// ADDDING SOUNDS
/// Each sequencer has an attached 'SequenecerData' a scriptable object, that contains 
/// all the samples that it can play 
/// These are all stored under Assets->Scripts->ScriptableObjectScripts
/// Click on whichever one you want to edit to add/remove sounds in the inspector
/// You can also find it by clicking on it via the inspector of the sequencer object if you
/// scroll to the bottom and click on the sequencer data object, it should pop up in project view
/// The samples are all stored in Assets->Audio->AudioSamples when you want to add new soudns to project
/// 
/// SPOKEN OBJECT NAMES
/// The sequencer plays the name of the object every four bars
/// For the spoken object names - these were all manually recorded and some effects applied to the voice
/// And then exported to the project. 
/// The 'delay' is happening in the project - it's not really a delay, the sequencer is just playing them 3 times in a row
/// The name is assigned onto the object prefab - on the falling object - SpokenName
///
///  
/// NOTE ON VOLUME
/// Currently the different audiosources on the sequencer objects have an initial volume 
/// that balances them out. 
/// this is true for the samples that are currently in the project 
/// but this is just to note that the volume of all the samples in each pool of samples should 
/// be similar, otherwise some sounds won't be that audible/or overpower the rest 
/// 
/// MELODIC CONTENT
/// Currently the melodic riffs just use the same sound
/// eg if the sample it picks for that object is an F# string it's just going to repeat that
/// until the next object falls. there are ways that this could be changed - but it would
/// require a lot more samples, and a restructuring to be able to have sets within the sample sets
/// and then the code to manage that 
/// This wasn't done at this point partly because it sounded like it had enough variation as it is
/// And didn't have the samples to do it 
/// 
/// </summary>



public class SequencerManager : MonoBehaviour
{
    [SerializeField]
    private ObjectManager _objectManager;

    //the different sequencer objects responsible for the different types of sounds in a track 
    [Header("Sequencers")]
    [SerializeField]
    private Sequencer _kickSequencer;
    [SerializeField]
    private Sequencer _snareSequencer; //snares or claps
    [SerializeField]
    private Sequencer _hiHatSequencer; //hihats or cymbols
    [SerializeField]
    private Sequencer _percSequencer; //misc percussive sounds, sfx 
    [SerializeField]
    private Sequencer _synthSequencer;
    [SerializeField]
    private Sequencer _bassSequencer;
    [SerializeField]
    private Sequencer _padSequencer; //sustained chord/ambience 
    [SerializeField]
    private Sequencer _lyricSequencer; //spoken name of object 
    [SerializeField]
    private Sequencer _emptySequencer; //using this to keep a clock from when objects start falling, it does not make any sounds but is running

    //this list is only for the 7 (4 percussive and 3 melodic) sequencers - the lyric and empty work differntly
    private List<Sequencer> _sequencerList = new List<Sequencer>();
 
    private int _currentBarCount = -1;
    private bool _firstObjectHasFallen = false;


    private void Awake()
    {
        //add all the sequencers except the empty one and lyrics one (handled differently) to sequencer list 
        _sequencerList.Add(_kickSequencer);
        _sequencerList.Add(_snareSequencer);
        _sequencerList.Add(_hiHatSequencer);
        _sequencerList.Add(_percSequencer);
        _sequencerList.Add(_synthSequencer);
        _sequencerList.Add(_bassSequencer);
        _sequencerList.Add(_padSequencer);

        //start by playing kick track
        RandomizePattern(_kickSequencer);
        AssignRandomSoundFromGroup(_kickSequencer);
        _kickSequencer.isMuted = false;

        for (int i = 1; i < _sequencerList.Count; i++)
        {
            _sequencerList[i].isMuted = true;
        }

        //subscibe to the on object landed event so we can make things happen each time an object lands 
        _objectManager.OnObjectLanded += OnObjectLanded;

        //make sure empty sequencer is not audible but is not muted (otherwise it won't run)
        _emptySequencer._audioSource.volume = 0;
        _emptySequencer.isMuted = false;

        //subscribe to on any step event so that you can count bars for things to happen only on certain bars 
        _emptySequencer.onAnyStep += BarCounter;
    }


    private void OnObjectLanded(FallingObject obj)
    {
        if (!_firstObjectHasFallen)
        {
            _firstObjectHasFallen = true;
        }

        CreateNewObjectSong();

        _currentBarCount = -1;
        //Debug.Log("on object land current seq index is " + _currentSequencerIndex);


        //set the correct spoken object name to match the object and unmute
        if (obj.SpokenName != null)
        {
            _lyricSequencer.isMuted = false;
            _lyricSequencer.SetAudioClip(obj.SpokenName);
        }
        else {
            _lyricSequencer.isMuted = true;
        }
    }

    //decides what sample to use for the sequencer
     private void AssignRandomSoundFromGroup(Sequencer seq)
     {
        SequencerDataScriptableObject seqData = seq.GetComponent<SequencerData>().sequencerData;

         //select a random sound from the list 
         AudioClip selectedSound = seqData.audioSamples[Random.Range(0, seqData.audioSamples.Count-1)];
         //assign sound to the sequencer
         seq.SetAudioClip(selectedSound);
     }

    //chooses a beat/pattern for the sequencer
    ///the pattern is just a list of bools
     private void RandomizePattern(Sequencer s)
     {
         for (int i = 0; i < s.sequence.Length - 1; i++)
         {
             int choice = Random.Range(0, 2);
             if (choice == 0)
             {
                 s.sequence[i] = false;
             }
             else
             {
                 s.sequence[i] = true;
             }
         }
     }
  

    //this is for counting bars for the lyric sequencer to be able to mute/unmute every 4 bars 
    //it changes an element of the song every bar
    private void BarCounter(int currentStep, int numberOfSteps)
    {
     //   Debug.Log("current beat " + currentStep + " number of beats " + numberOfSteps);
      //  Debug.Log("current bar count " + _currentBarCount);
       if (_firstObjectHasFallen)
        {
            if (currentStep == 1) //every bar
            {
                _currentBarCount++;
               // Debug.Log(" over four " + _currentBarCount % 4);
                //every 4 bars
                if (_currentBarCount % 4 == 0)
                {
                    _lyricSequencer.isMuted = false;
                }
                else
                {
                    _lyricSequencer.isMuted = true;
                   // Debug.Log("MUTITNG LYRIC SEQUENCER");
                }

                ChangeElementOfSong();
            }
        }
    }

    //this will totally shuffle the patterns of each sequencer, the sounds assigned to them, and which are unmuted/muted
    //used when a new object lands
    private void CreateNewObjectSong()
    {
        //decide whether or not to mute or unmute sequencer 

        for (int i = 0; i < _sequencerList.Count; i++)
        {
            AssignRandomSoundFromGroup(_sequencerList[i]);
            RandomizePattern(_sequencerList[i]);

            int choice = Random.Range(0, 2);
            if (choice == 0)
            {
                _sequencerList[i].isMuted = false;
            }
            else
            {
                _sequencerList[i].isMuted = true;
            }
        }

        //limit melodic seqs because it sounds too much when they're all playing at once
        LimitMelodicCacophony();

        if (GetActiveTrackCount() == 0)
        {
            UnmuteRandomTrack();
        }
        if (GetActiveTrackCount() == 1)
        {
                   
            //if it's not a melodic track add kick because it sounds weird if it's just a hi-hat, for example
            if (_padSequencer.isMuted && _synthSequencer.isMuted && _bassSequencer.isMuted)
            {
                _kickSequencer.isMuted = false;
            }
        }
    }

    //limit melodic seqs because it sounds too much when they're all playing at once
    //turns one off if all three are on at once
    private void LimitMelodicCacophony()
    {
        if (!_padSequencer.isMuted && !_bassSequencer.isMuted && !_synthSequencer.isMuted)
        {
            int choice = Random.Range(0, 3);
            if (choice == 0)
            {
                _padSequencer.isMuted = true;
            }
            else if (choice == 1)
            {
                _bassSequencer.isMuted = true;
            }
            else if (choice == 2)
            {
                _synthSequencer.isMuted = true;
            }
        }
    }

    //is called every bar
    private void ChangeElementOfSong()
    {
       // Debug.Log("changing element of song");

        //first we mute or unmute a random track 
        //if there are only a few active we unmute one 

        if (GetActiveTrackCount() <= 2)
        {
            UnmuteRandomTrack();
        }
        //if there are a lot active we mute one
        else if (GetActiveTrackCount() >= 5)
        {
            MuteRandomTrack();
        }
        //or inbetween, we randomly decide 
        else
        {
            int choice = Random.Range(0, 2);

            if (choice == 0)
            {
                UnmuteRandomTrack();
            }
            else
            {
                MuteRandomTrack();
            }
        }

        //then we change the pattern of something that's not muted
        ShufflePatternOfRandomTrack();
       

        //checking that we don't have more than two melodic things happening at once
        LimitMelodicCacophony();
    }


    //changes the pattern of a single sequencer
    private void ShufflePatternOfRandomTrack()
    {
        int trackToShuffle = 0;
        bool selectedUnmutedTrack = false;
        //check if they're not muted so we're not changing one we can't hear
        while (!selectedUnmutedTrack)
        {
            int ranNum = Random.Range(0, 7);
            if (!_sequencerList[ranNum].isMuted)
            {
                trackToShuffle = ranNum;
                selectedUnmutedTrack = true;
            }
        }
        RandomizePattern(_sequencerList[trackToShuffle]);
      //  Debug.Log("shuffling pattern of track " + trackToShuffle);
    }

    //checks to see how many tracks are umuted 
    private int GetActiveTrackCount()
    {
        int activeTrackCount = 0;

        for (int i = 0; i < _sequencerList.Count; i++)
        {
            if (!_sequencerList[i].isMuted)
            {
                activeTrackCount++;
            }
        }
        return activeTrackCount;
    }

    private void UnmuteRandomTrack()
    {
        int trackToUnmute =0;
        bool foundMutedTrack = false;
        while (!foundMutedTrack)
        {
            int ranNum = Random.Range(0, 7);
         if (_sequencerList[ranNum].isMuted)
            {
                trackToUnmute = ranNum;
                foundMutedTrack = true; 
            }
         
        }
        _sequencerList[trackToUnmute].isMuted = false;

      //  Debug.Log("unmuting track " + trackToUnmute);
    }

    private void MuteRandomTrack()
    {
        int trackToMute = 0;
        bool foundUnmutedTrack = false;
        while (!foundUnmutedTrack)
        {
            int ranNum = Random.Range(0, 7);
            if (!_sequencerList[ranNum].isMuted)
            {
                trackToMute = ranNum;
                foundUnmutedTrack = true;
            }
        }

        _sequencerList[trackToMute].isMuted = true;

       // Debug.Log("muting track " + trackToMute);
    }

}
