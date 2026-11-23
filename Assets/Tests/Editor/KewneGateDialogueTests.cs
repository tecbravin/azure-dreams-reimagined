using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AzureDreamsReimagined.Tests
{
    public class KewneGateDialogueTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private TowerGameController _controller;
        private Type _meetingType;
        private Type _questionType;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("Kewne gate dialogue test");
            _root.SetActive(false);
            _controller = _root.AddComponent<TowerGameController>();
            _meetingType = typeof(TowerGameController).GetNestedType("KewneMeeting", BindingFlags.NonPublic);
            _questionType = typeof(TowerGameController).GetNestedType("KewneGateQuestion", BindingFlags.NonPublic);
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void SuspicionAndRefusalsLeadThroughNameAndTutorialQuestions()
        {
            Invoke("BeginKewneGateDialogue");
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Introduction"));
            Assert.That(Get("_kewneIntroPage"), Is.EqualTo(0));
            AdvanceIntroductionTo("Question");
            Assert.That(State("_kewneGateQuestion"), Is.EqualTo("Reason"));
            Assert.That(QuestionHasChoices(), Is.True);

            Answer(1); // suspicious
            AdvanceIntroductionTo("Question");
            Answer(1); // alone: no
            AdvanceIntroductionTo("Question");
            Assert.That(State("_kewneGateQuestion"), Is.EqualTo("Join"));
            Answer(1); // initial refusal
            AdvanceIntroductionTo("Question");
            Answer(1); // reject the repeated offer
            AdvanceIntroductionTo("Question");
            Assert.That(State("_kewneGateQuestion"), Is.EqualTo("JoinAgain"));
            Answer(0); // accept
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Naming"));
            Assert.That(QuestionHasChoices(), Is.True);

            Set("_kewneNameDraft", "  River  ");
            Invoke("ConfirmKewneName");
            Assert.That((string)Get("_kewneNameDraft"), Is.EqualTo("River"));
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Introduction"));
            Assert.That(((string)Get("_dialogueLine")), Does.Contain("River"));
            AdvanceIntroductionTo("Question");
            Assert.That(State("_kewneGateQuestion"), Is.EqualTo("HeroName"));
            var answers = Invoke("KewneGateAnswers");
            Assert.That(answers.ToString(), Does.Contain("HeroName"));
            Answer(0);
            Assert.That(State("_kewneGateQuestion"), Is.EqualTo("Tutorial"));
            Assert.That(((string)Get("_dialogueLine")), Does.Contain("HeroName"));
            Answer(0);
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Reply"));
            Assert.That(Get("_kewneTutorialChoice"), Is.EqualTo((bool?)true));
        }

        [Test]
        public void BlankNameDefaultsAndNoTutorialChoiceIsStoredAsFalse()
        {
            Invoke("BeginKewneGateDialogue");
            AdvanceIntroductionTo("Question");
            Answer(0);
            AdvanceIntroductionTo("Question");
            Answer(0);
            AdvanceIntroductionTo("Question");
            Answer(0);
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Naming"));
            Set("_kewneNameDraft", "   ");
            Invoke("ConfirmKewneName");
            Assert.That((string)Get("_kewneNameDraft"), Is.EqualTo("Kewne"));
            AdvanceIntroductionTo("Question");
            var answers = Invoke("KewneGateAnswers");
            Assert.That(answers.ToString(), Does.Contain("HeroName"));
            Answer(1);
            Answer(1);
            Assert.That(State("_kewneMeeting"), Is.EqualTo("Reply"));
            Assert.That(Get("_kewneTutorialChoice"), Is.EqualTo((bool?)false));
            Assert.That(QuestionHasChoices(), Is.False);
        }

        private void AdvanceIntroductionTo(string expected)
        {
            for (int i = 0; i < 40 && State("_kewneMeeting") == "Introduction"; i++)
                Invoke("AdvanceKewneMeeting", 0);
            Assert.That(State("_kewneMeeting"), Is.EqualTo(expected), "Introduction did not reach the expected state within 40 pages.");
        }

        private void Answer(int selection) => Invoke("AnswerKewneGateQuestion", selection);
        private bool QuestionHasChoices() => (bool)typeof(TowerGameController).GetProperty("KewneMeetingHasChoices", Private).GetValue(_controller);
        private string State(string field) => Get(field).ToString();
        private object Get(string name) => typeof(TowerGameController).GetField(name, Private).GetValue(_controller);
        private void Set(string name, object value) => typeof(TowerGameController).GetField(name, Private).SetValue(_controller, value);
        private object Invoke(string name, params object[] args) => typeof(TowerGameController).GetMethod(name, Private).Invoke(_controller, args);
    }
}

