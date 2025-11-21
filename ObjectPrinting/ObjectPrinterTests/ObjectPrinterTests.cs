using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using FluentAssertions;

namespace ObjectPrinting.Tests;

[TestFixture]
public class ObjectPrinterStandardSerializationTests
{
    private Person? setUpPerson;

    [OneTimeSetUp]
    public void Setup()
    {
        setUpPerson = new Person
        {
            Id = Guid.Parse("f14dd761-3260-4463-a4ad-6ba14de2026c"),
            Name = "Fai",
            Height = 160.5,
            Age = 20,
            Scores = [95, 88, 76],
            Tags = ["brooch", "Nevada"],
            Ratings = new Dictionary<string, int>
            {
                ["quality"] = 5,
                ["speed"] = 4
            },
            Friend = new Person
            {
                Id = Guid.Parse("cd55ace6-ac55-4f85-9434-82672b2fd9ee"),
                Name = "Sigma",
                Height = 180.3,
                Age = 81,
                Scores = [93, 55, 99, 32],
                Tags = ["robots", "moon", "zero"],
                Ratings = new Dictionary<string, int>(),
                Friend = null
            }
        };
    }

    [Test]
    public void PrintToString_ShouldIncludeBasicProperties_WhenPersonIsValid()
    {
        var actual = setUpPerson.PrintToString();

        actual.Should().NotBeNullOrEmpty();
        actual.Should().Contain("Id = f14dd761-3260-4463-a4ad-6ba14de2026c");
        actual.Should().Contain("Person");
        actual.Should().Contain("Name = Fai");
        actual.Should().Contain("Age = 20");
        actual.Should().Contain("Height = 160,5");
        actual.Should().Contain("Tags = List");
        actual.Should().Contain("Ratings = Dictionary");
        actual.Should().Contain("Friend = Person");
        actual.Should().Contain("Friend = null");
    }


    [Test]
    public void PrintToString_ShouldApplyMemberExclusion_WhenNameExcludedInConfig()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Excluding(p => p.Name));

        actual.Should().NotContain("Name = Fai");
        actual.Should().Contain("Id = f14dd761-3260-4463-a4ad-6ba14de2026c");
    }

    [Test]
    public void PrintToString_ShouldApplyTypeExclusion_WhenIntTypeExcludedInConfig()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Excluding<int>());

        actual.Should().NotContain("Age = 20");
        actual.Should().Contain("Id = f14dd761-3260-4463-a4ad-6ba14de2026c");
    }

    [Test]
    public void PrintToString_ShouldUseCustomFormatter_WhenIntTypeFormatterIsConfigured()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing<int>().Using(n => $"Age: {n}"));

        actual.Should().Contain("Age: 20");
    }

    [Test]
    public void PrintToString_ShouldUseInvariantCulture_WhenDoubleCultureIsSetToInvariant()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing<double>().Using(CultureInfo.InvariantCulture));

        actual.Should().Contain("Height = 160.5");
    }

    [Test]
    public void PrintToString_ShouldApplyMemberSerializer_WhenNameCustomSerializerIsSet()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing(p => p.Name).Using(p => p.ToUpper()));

        actual.Should().Contain("Name = FAI");
    }

    [Test]
    public void PrintToString_ShouldTrimStringProperty_WhenNameTrimmedToLengthSpecified() // pomenyat
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing(p => p.Name).TrimmedToLength(2));

        actual.Should().Contain("Name = Fa");
    }

    [Test]
    public void PrintToString_ShouldApplyMultipleCustomizations_WhenCombinedExclusionsAndSerializersUsed()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Excluding<Guid>()
            .Excluding(p => p.Tags)
            .Printing<int>().Using(n => $"Number: {n}")
            .Printing<double>().Using(CultureInfo.InvariantCulture)
            .Printing(p => p.Name).TrimmedToLength(2)
            .Printing(p => p.Name).Using(p => p.ToUpper()));

        actual.Should().NotContain("Guid");
        actual.Should().NotContain("Tags");
        actual.Should().Contain("Number: 20");
        actual.Should().NotContain("Age = 20");
        actual.Should().Contain("Height = 160.5");
        actual.Should().NotContain("Heigh = 160,5");
        actual.Should().Contain("Name = FA");
    }

    [Test]
    public void PrintToString_ShouldRenderIntArrayWithIndexedItems_WhenPersonHasArray()
    {
        var actual = setUpPerson.PrintToString();

        actual.Should().Contain("Scores = Int32[]");
        actual.Should().Contain("[0] = 95");
        actual.Should().Contain("[1] = 88");
        actual.Should().Contain("[2] = 76");
    }

    [Test]
    public void PrintToString_ShouldRenderGenericListWithIndexedItems_WhenPersonHasList()
    {
        var actual = setUpPerson.PrintToString();

        actual.Should().Contain("Tags = List<String>");
        actual.Should().Contain("[0] = brooch");
        actual.Should().Contain("[1] = Nevada");
    }

    [Test]
    public void PrintToString_ShouldRenderDictionaryWithKeyValuePairs_WhenPersonHasDictionary()
    {
        var actual = setUpPerson.PrintToString();

        actual.Should().Contain("Ratings = Dictionary<String, Int32>");
        actual.Should().Contain("Key = quality");
        actual.Should().Contain("Value = 5");
        actual.Should().Contain("Key = speed");
        actual.Should().Contain("Value = 4");
    }

    [Test]
    public void PrintToString_ShouldRenderMultidimensionalArrayRank_WhenArrayIsTwoDimensional()
    {
        var matrix = new[,]
        {
            { 1, 1, 1 },
            { 1, 1, 1 }
        };

        var actual = matrix.PrintToString();

        actual.Should().Contain("Int32[,]");
    }

    [Test]
    public void PrintToString_ShouldRenderJaggedArraySuffixes_WhenArrayIsArrayOfArrays()
    {
        var jagged = new int[][]
        {
            [1, 2, 3],
            [4, 5, 6]
        };

        var actual = jagged.PrintToString();

        actual.Should().Contain("Int32[][]");
    }

    [Test]
    public void PrintToString_ShouldUseCustomSerializer_WhenIntArraySerializerIsConfigured()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing<int[]>()
            .Using(arr => $"int[{arr.Length}] {{ {string.Join(", ", arr)} }}"));

        actual.Should().Contain("Scores = int[3] { 95, 88, 76 }");
    }

    [Test]
    public void PrintToString_ShouldUseCustomSerializer_WhenStringListSerializerIsConfigured()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing<List<string>>()
            .Using(list => $"List<string> (Count = {list.Count}) [ {string.Join(", ", list)} ]"));

        actual.Should().Contain("Tags = List<string> (Count = 2) [ brooch, Nevada ]");
    }

    [Test]
    public void PrintToString_ShouldUseCustomSerializer_WhenDictionarySerializerIsConfigured()
    {
        var actual = setUpPerson.PrintToString(cfg => cfg
            .Printing<Dictionary<string, int>>()
            .Using(dict =>
            {
                var pairs = dict.Select(kv => $"{kv.Key}: {kv.Value}");
                return $"Dictionary<string,int> (Count = {dict.Count}) {{ {string.Join(", ", pairs)} }}";
            }));

        actual.Should().Contain("Ratings = Dictionary<string,int> (Count = 2) { quality: 5, speed: 4 }");
    }

    [Test]
    public void PrintToString_ShouldEmitCycleMarker_WhenObjectGraphHasCyclicReference()
    {
        var person = new Person();
        person.Friend = person;

        var act = () => person.PrintToString();
        var actual = person.PrintToString();

        act.Should().NotThrow();
        actual.Should().Contain("CyclicRef");
    }

    [Test]
    public void PrintToString_ShouldReportNestingLevelExceeded_WhenFriendChainDepthBeyondMax()
    {
        var person = new Person();
        person.Friend = new Person();
        person.Friend.Friend = new Person();

        var printer = new PrintingConfig<Person>
        {
            MaxNestingLevel = 2
        };

        var actual = printer.PrintToString(person);

        actual.Should().Contain("превышен уровень вложенности");
    }
}