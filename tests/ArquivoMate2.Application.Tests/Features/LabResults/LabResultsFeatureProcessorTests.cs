using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using ArquivoMate2.Application.Features.Processors.LabResults;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain.Parsing;
using ArquivoMate2.Application.Features.Processors.LabResults.Domain;
using ArquivoMate2.Application.Interfaces;
using Marten;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ArquivoMate2.Application.Tests.Features.LabResults
{
    public class LabResultsFeatureProcessorTests
    {
        private static LabResultsFeatureProcessor CreateSut()
        {
            var queryMock = new Mock<IQuerySession>();
            var sessionMock = new Mock<IDocumentSession>();
            var storageMock = new Mock<IStorageProvider>();
            var metaMock = new Mock<IFileMetadataService>();
            var loggerMock = new Mock<ILogger<LabResultsFeatureProcessor>>();

            return new LabResultsFeatureProcessor(queryMock.Object, sessionMock.Object, storageMock.Object, metaMock.Object, loggerMock.Object);
        }

        [Fact]
        public void ProcessRawData_ParsesValidInputWithoutThrowing()
        {
            // arrange
            var sut = CreateSut();

            var report = new LabReport
            {
                LabName = "TestLab",
                Patient = "John Doe",
                Values = new List<ValueColumn>
                {
                    new ValueColumn
                    {
                        Date = "2024-01-02",
                        Measurements = new List<Measurement>
                        {
                            new Measurement { Parameter = "Glucose", Result = "<0.6", Unit = "mmol/L", Reference = "0.50-1.25" },
                            new Measurement { Parameter = "Hb", Result = "13.2", Unit = "g/dL", Reference = ">= 12" }
                        }
                    }
                }
            };

            var docId = Guid.NewGuid();

            // act / assert: should not throw
            var method = typeof(LabResultsFeatureProcessor).GetMethod("ProcessRawData", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var ex = Record.Exception(() => method.Invoke(sut, new object[] { report, docId }));
            // Invocation may wrap inner exceptions - ensure none
            Assert.Null(ex);

            // verify returned results
            var ret = method.Invoke(sut, new object[] { report, docId });
            Assert.NotNull(ret);
            var list = ret as List<LabResult>;
            Assert.NotNull(list);
            Assert.Single(list);
            var lr = list![0];
            Assert.Equal("TestLab", lr.LabName);
            Assert.Equal("John Doe", lr.Patient);
            Assert.Equal(DateOnly.Parse("2024-01-02"), lr.Date);
            Assert.Equal(2, lr.Points.Count);

            var p1 = lr.Points[0];
            Assert.Equal("<", p1.ResultComparator);
            Assert.Equal(0.6m, p1.ResultNumeric);
            Assert.Equal("mmol/l", p1.NormalizedUnit);
            Assert.Equal(0.50m, p1.ReferenceFrom);
            Assert.Equal(1.25m, p1.ReferenceTo);

            var p2 = lr.Points[1];
            Assert.Null(p2.ResultComparator);
            Assert.Equal(13.2m, p2.ResultNumeric);
            Assert.Equal(">=", p2.ReferenceComparator);
            Assert.Equal(12m, p2.ReferenceFrom);
        }

        [Fact]
        public void ProcessRawData_InvalidDate_ThrowsFormatException()
        {
            // arrange
            var sut = CreateSut();

            var report = new LabReport
            {
                LabName = "TestLab",
                Patient = "Jane Roe",
                Values = new List<ValueColumn>
                {
                    new ValueColumn
                    {
                        Date = "02.01.2024", // invalid format for yyyy-MM-dd
                        Measurements = new List<Measurement>
                        {
                            new Measurement { Parameter = "Glucose", Result = "1.0", Unit = "mmol/L", Reference = "0.5-1.5" }
                        }
                    }
                }
            };

            var docId = Guid.NewGuid();

            var method = typeof(LabResultsFeatureProcessor).GetMethod("ProcessRawData", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);

            var tie = Assert.Throws<TargetInvocationException>(() => method.Invoke(sut, new object[] { report, docId }));
            Assert.IsType<FormatException>(tie.InnerException);
        }
    }
}
