﻿
using LavishScriptAPI;
using Questor.Modules.BackgroundTasks;
using Questor.Modules.Caching;
using Questor.Modules.Lookup;
using Questor.Modules.States;

namespace Questor.Modules.Actions
{
    using System;
    using System.Collections.Generic;
    using System.Windows.Forms;
    using Questor.Modules.Logging;
    
    public static class SkillTrainerClass
    {
        //private static bool QuitEVEWhenDone = false;
        //private static bool CloseSkillTrainerWhenDone = false;
        //private static bool _showHelp;

        private static DateTime _nextSkillTrainerProcessState;
        private static DateTime _nextSkillTrainerAction = DateTime.MinValue;
        //private static readonly bool _quitEveWhenDone;
        //private static readonly bool _closeSkillTrainerWhenDone;

        static SkillTrainerClass()
        {
            Logging.Log("SkillTrainer", "Starting SkillTrainer", Logging.Orange);
            _State.CurrentSkillTrainerState = SkillTrainerState.Idle;
            //_quitEveWhenDone = QuitEVEWhenDone;
            //_closeSkillTrainerWhenDone = CloseSkillTrainerWhenDone;
        }

        public static void ProcessState()
        {
            // Only pulse state changes every .5s
            if (DateTime.UtcNow <  _nextSkillTrainerProcessState) //default: 2000ms
            {
                return;
            }
            
            _nextSkillTrainerProcessState = DateTime.UtcNow.AddMilliseconds(Time.Instance.SkillTrainerPulse_milliseconds);

            switch (_State.CurrentSkillTrainerState)
            {
                case SkillTrainerState.Idle:
                    if (Cache.Instance.InStation && DateTime.UtcNow > _nextSkillTrainerAction)
                    {
                        Logging.Log("SkillTrainer", "It is Time to Start SkillTrainer again...", Logging.White);
                        _State.CurrentSkillTrainerState = SkillTrainerState.Begin;
                    }
                    break;

                case SkillTrainerState.Begin:
                    _State.CurrentSkillTrainerState = SkillTrainerState.LoadPlan;
                    break;

                case SkillTrainerState.LoadPlan:
                    Logging.Log("SkillTrainer", "LoadPlan", Logging.Debug);
                    if (!SkillPlan.ImportSkillPlan())
                    {
                        _State.CurrentSkillTrainerState = SkillTrainerState.Error;
                        return;
                    }

                    SkillPlan.ReadySkillPlan();
                    _State.CurrentSkillTrainerState = SkillTrainerState.ReadCharacterSheetSkills;
                    break;

                case SkillTrainerState.ReadCharacterSheetSkills:
                    Logging.Log("SkillTrainer", "ReadCharacterSheetSkills", Logging.Debug);
                    if (!SkillPlan.ReadMyCharacterSheetSkills()) return;

                    _State.CurrentSkillTrainerState = SkillTrainerState.CheckTrainingQueue;
                    break;

                //case SkillTrainerState.AreThereSkillsReadyToInject:
                //    Logging.Log("SkillTrainer", "AreThereSkillsReadyToInject", Logging.Debug);
                //    if (!Skills.AreThereSkillsReadyToInject()) return;
                //
                //    _State.CurrentSkillTrainerState = SkillTrainerState.CheckTrainingQueue;
                //    break;

                case SkillTrainerState.CheckTrainingQueue:
                    Logging.Log("SkillTrainer", "CheckTrainingQueue", Logging.Debug);
                    if (!SkillPlan.RetrieveSkillQueueInfo()) return;
                    if (!SkillPlan.CheckTrainingQueue("SkillTrainer")) return;

                    _State.CurrentSkillTrainerState = SkillTrainerState.Done;
                    break;

                case SkillTrainerState.CloseQuestor:
                    Logging.Log("Startup", "Done Training: Closing EVE", Logging.Orange);
                    Cache.Instance.CloseQuestorCMDLogoff = false;
                    Cache.Instance.CloseQuestorCMDExitGame = true;
                    Cache.Instance.CloseQuestorEndProcess = true;
                    Settings.Instance.AutoStart = false;
                    Cache.Instance.ReasonToStopQuestor = "Done Processing Skill Training Plan and adding skills as needed to the training queue";
                    Cache.Instance.SessionState = "Quitting";
                    Cleanup.CloseQuestor(Cache.Instance.ReasonToStopQuestor);
                    break;

                case SkillTrainerState.GenerateInnerspaceProfile:
                    Logging.Log("SkillTrainer", "Generating Innerspace Profile for this toon: running [GenerateInnerspaceProfile.iss] from your innerspace scripts directory", Logging.Teal);
                    LavishScript.ExecuteCommand("echo runscript GenerateInnerspaceProfile \"" + Settings.Instance.CharacterName + "\"");
                    LavishScript.ExecuteCommand("runscript GenerateInnerspaceProfile \"" + Settings.Instance.CharacterName + "\"");
                    _State.CurrentSkillTrainerState = SkillTrainerState.Idle;
                    break;

                case SkillTrainerState.Done:
                    //if (_quitEveWhenDone)
                    //{
                    //    _State.CurrentSkillTrainerState = SkillTrainerState.CloseQuestor;
                    //    return;
                    //}

                    //if (_closeSkillTrainerWhenDone)
                    //{
                    //    //Close SkillTrainer - without closing eve here
                    //    Application.Exit();
                    //    return;
                    //}
                    SkillPlan.injectSkillBookAttempts = 0;
                    _nextSkillTrainerAction = DateTime.UtcNow.AddHours(Cache.Instance.RandomNumber(3, 4));
                    _State.CurrentSkillTrainerState = SkillTrainerState.Idle;
                    _States.CurrentQuestorState = QuestorState.Idle;
                    break;
            }
        }
    }
}