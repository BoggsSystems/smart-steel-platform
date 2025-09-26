# Quality Control Service - Unit Tests

Comprehensive unit testing suite for the Quality Control Service, covering ASTM A615/A706 compliance testing, controllers, services, and models with enterprise-grade testing patterns.

## 🧪 Test Coverage

### **Target Coverage: 85%**

| Component | Test Methods | Coverage Target | Status |
|-----------|-------------|-----------------|--------|
| **Controllers** | 35 tests | 90% | ✅ Complete |
| **Services** | 25 tests | 85% | ✅ Complete |
| **Models** | 20 tests | 80% | ✅ Complete |

## 📁 Test Structure

```
tests/
├── Unit/
│   ├── Controllers/
│   │   └── QualityControlControllerTests.cs    # API endpoint testing
│   ├── Services/
│   │   └── QualityControlDataServiceTests.cs   # Business logic testing
│   └── Models/
│       └── QualityControlModelsTests.cs        # Domain model testing
├── Fixtures/
│   └── QualityTestFixture.cs                   # Test data and utilities
├── coverlet.runsettings                         # Coverage configuration
└── README.md                                   # This file
```

## 🚀 Running Tests

### **Basic Test Execution**
```bash
# Run all tests
dotnet test

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"

# Run specific test class
dotnet test --filter "FullyQualifiedName~QualityControlControllerTests"
```

### **Code Coverage**
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings

# Generate coverage with specific format
dotnet test --collect:"XPlat Code Coverage" --settings coverlet.runsettings -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover
```

## 📊 Test Categories

### **1. Controller Tests (QualityControlControllerTests)**
- **HTTP Endpoints**: GET, POST operations for samples and test results
- **Status Validation**: OK, NotFound, BadRequest, Created responses
- **Error Handling**: Exception propagation, null handling
- **ASTM Compliance**: Validation workflows, compliance checking
- **Performance**: Large data sets, concurrent requests

**Key Test Methods:**
- `GetSample_ExistingSample_ReturnsOkWithSample()`
- `CreateSample_ValidRequest_ReturnsCreatedAtActionWithSample()`
- `AddTensileTestResult_ValidRequest_ReturnsOkWithResult()`
- `ValidateASTMCompliance_CompliantSample_ReturnsOk()`
- `GetQualityDashboardSummary_ValidData_ReturnsOkWithSummary()`

### **2. Service Tests (QualityControlDataServiceTests)**
- **Cosmos DB Integration**: CRUD operations, error handling
- **ASTM Compliance Testing**: A615/A706 standard validation
- **Test Result Processing**: Tensile, bend, dimensional, chemical tests
- **Station Management**: Equipment tracking, workload management
- **Alert System**: Quality alerts, resolution workflows

**Key Test Methods:**
- `GetSampleAsync_ExistingSample_ReturnsSample()`
- `ValidateASTMComplianceAsync_CompliantSample_ReturnsSuccess()`
- `ValidateASTMComplianceAsync_A706Standard_ValidatesCorrectLimits()`
- `AddTestResultAsync_ValidTensileTest_AddsResult()`
- `GetASTMSpecificationAsync_Grade60A615_ReturnsCorrectSpec()`

### **3. Model Tests (QualityControlModelsTests)**
- **Data Validation**: Property assignments, default values
- **ASTM Standards**: Specification generation, compliance rules
- **Business Logic**: Test result evaluation, quality scoring
- **Serialization**: JSON round-trip testing
- **Integration**: Model interactions, complete workflows

**Key Test Methods:**
- `QualityTestSample_PropertyAssignment_WorksCorrectly()`
- `TensileTestResult_ValidGrade60Values_MeetsASTMRequirements()`
- `ASTMSpecification_GetSpecification_Grade60A615_ReturnsCorrectValues()`
- `QualityAlert_GetRecommendedAction_ReturnsCorrectActions()`
- `QualityTestSample_CompleteWorkflow_HandlesAllTestTypes()`

## 🎯 Test Data & Fixtures

### **QualityTestFixture Class**
Provides standardized test data generation for quality control testing:

```csharp
// Generate valid quality test sample
var sample = _fixture.CreateValidQualityTestSample("QTS-0001", RebarGrade.Grade60, RebarSize.Size5);

// Generate compliant tensile test result
var tensileResult = _fixture.CreateValidTensileTestResult(grade: RebarGrade.Grade60);

// Generate ASTM specification
var spec = _fixture.CreateASTMSpecification(ASTMStandard.A615, RebarGrade.Grade60, RebarSize.Size5);

// Generate quality control station
var station = _fixture.CreateValidQualityControlStation("QC-STN-01");
```

### **ASTM Compliance Test Data**
- **Grade 40**: 275 MPa yield, 420 MPa tensile, 12% elongation
- **Grade 60**: 420 MPa yield, 620 MPa tensile, 9% elongation  
- **Grade 75**: 520 MPa yield, 690 MPa tensile, 7% elongation
- **Grade 80**: 550 MPa yield, 720 MPa tensile, 6% elongation

### **Chemical Composition Limits**
- **A615 Standard**: C ≤ 0.30%, Mn ≤ 1.50%, P ≤ 0.040%, S ≤ 0.050%
- **A706 Standard**: C ≤ 0.25%, Mn ≤ 1.35%, P ≤ 0.035%, S ≤ 0.045%

### **Dimensional Requirements**
- **Diameter Tolerance**: ±0.5mm for all sizes
- **Rib Height**: Minimum 4.3% of nominal diameter
- **Relative Rib Area**: Minimum 5.5% of cross-section
- **Bend Test**: 180° without cracking (3d pin for ≤#10, 4d for >#10)

## 🛠️ Testing Frameworks

### **Primary Dependencies**
```xml
<PackageReference Include="xunit" Version="2.4.2" />
<PackageReference Include="Moq" Version="4.20.69" />
<PackageReference Include="FluentAssertions" Version="6.12.0" />
<PackageReference Include="AutoFixture" Version="4.18.0" />
```

### **Testing Patterns**
- **AAA Pattern**: Arrange, Act, Assert
- **Mock Objects**: Service dependencies, Cosmos DB containers
- **Test Doubles**: In-memory implementations
- **Data Builders**: Fluent test data creation with realistic values
- **Theory Tests**: Parameterized tests for multiple scenarios

## 📈 Quality Metrics

### **Performance Targets**
- **Test Execution**: <15 seconds for full suite
- **Individual Tests**: <200ms each
- **Memory Usage**: <150MB peak during execution
- **Coverage**: >85% line coverage

### **Quality Gates**
- ✅ All tests must pass
- ✅ No compiler warnings
- ✅ Code coverage targets met
- ✅ Performance benchmarks achieved
- ✅ ASTM compliance validation complete

## 🔬 ASTM Compliance Testing

### **ASTM A615 Standard Testing**
- Mechanical properties validation
- Chemical composition limits
- Dimensional requirements
- Bend test compliance
- Surface quality assessment

### **ASTM A706 Standard Testing (Seismic Applications)**
- Stricter chemical limits
- Yield-to-tensile ratio requirements
- Enhanced ductility requirements
- Special marking requirements

### **Test Result Validation**
```csharp
[Theory]
[InlineData(RebarGrade.Grade40, 275, 420, 12.0)]
[InlineData(RebarGrade.Grade60, 420, 620, 9.0)]
[InlineData(RebarGrade.Grade75, 520, 690, 7.0)]
[InlineData(RebarGrade.Grade80, 550, 720, 6.0)]
public async Task ValidateASTMCompliance_DifferentGrades_CorrectMechanicalProperties(
    RebarGrade grade, double expectedYield, double expectedTensile, double expectedElongation)
```

## 🔍 Debugging Tests

### **Visual Studio**
- Set breakpoints in test methods
- Use Test Explorer for individual test runs
- Debug specific ASTM compliance failures

### **VS Code**
- Install .NET Test Explorer extension
- Use integrated debugger
- View test output in terminal

### **Command Line**
```bash
# Debug specific test
dotnet test --filter "ValidateASTMCompliance_CompliantSample_ReturnsSuccess" --logger "console;verbosity=diagnostic"

# Run ASTM compliance tests only
dotnet test --filter "Category=ASTM" --logger "console;verbosity=detailed"
```

## 🚨 Troubleshooting

### **Common Issues**

1. **Cosmos DB Mock Setup**
   ```csharp
   // Ensure proper container mock setup
   _mockCosmosClient
       .Setup(c => c.GetContainer("SteelMillRebarDB", "QualityTestSamples"))
       .Returns(_mockSamplesContainer.Object);
   ```

2. **ASTM Specification Caching**
   ```csharp
   // Verify specification cache behavior
   var spec1 = await _service.GetASTMSpecificationAsync(standard, grade, size);
   var spec2 = await _service.GetASTMSpecificationAsync(standard, grade, size);
   spec1.Should().BeSameAs(spec2); // Cached result
   ```

3. **Test Result Compliance**
   ```csharp
   // Ensure test results meet ASTM requirements
   tensileResult.YieldStrength.Should().BeGreaterOrEqualTo(420); // Grade 60 minimum
   tensileResult.ElongationAtBreak.Should().BeGreaterOrEqualTo(9.0);
   ```

### **Performance Issues**
- Check test data generation efficiency
- Verify mock setup overhead
- Monitor memory usage with large datasets
- Validate ASTM compliance calculation performance

## 📋 Maintenance

### **Adding New Tests**
1. Follow existing naming conventions
2. Use QualityTestFixture for data generation
3. Include both positive and negative test cases
4. Add ASTM compliance validation tests
5. Include performance tests for new features

### **Updating ASTM Standards**
1. Modify ASTMSpecification.GetSpecification method
2. Update test data in QualityTestFixture
3. Add new validation test cases
4. Update documentation accordingly

### **Coverage Improvements**
1. Identify uncovered code paths
2. Add edge case testing for ASTM limits
3. Include error condition scenarios
4. Validate boundary conditions for all grades

## 🎯 Next Steps

- [ ] Add integration tests for external dependencies
- [ ] Implement load testing scenarios for quality stations
- [ ] Add mutation testing for ASTM compliance validation
- [ ] Create automated test report generation
- [ ] Set up continuous integration pipeline
- [ ] Add stress testing for concurrent quality operations

## 🏗️ ASTM Standards Covered

### **Mechanical Properties**
- ✅ Yield strength testing (all grades)
- ✅ Tensile strength testing (all grades)
- ✅ Elongation testing (gauge length specific)
- ✅ Yield-to-tensile ratio (A706 specific)

### **Chemical Composition**
- ✅ Carbon content limits (A615/A706)
- ✅ Manganese content validation
- ✅ Phosphorus and sulfur limits
- ✅ Silicon content requirements

### **Physical Properties**
- ✅ Dimensional tolerance testing
- ✅ Rib pattern validation
- ✅ Surface quality assessment
- ✅ Bend test compliance (180°)

---

**🔬 Quality Control Service Tests - ASTM Compliant & Enterprise Ready! ✅**