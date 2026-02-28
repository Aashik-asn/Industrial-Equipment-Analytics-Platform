import { gql } from '@apollo/client';

// Simple plants query for filter dropdown, sidebar, and quick access
export const PLANTS_QUERY = gql`
  query Plants {
    plants {
      plantId
      plantName
    }
  }
`;

// Alias for backward compatibility
export const PLANTS_SIMPLE_QUERY = PLANTS_QUERY;

// Dashboard query - uses proper GraphQL types (UUID, DateTime)
export const DASHBOARD_QUERY = gql`
  query Dashboard($plantId: UUID, $from: DateTime, $to: DateTime) {
    dashboard(plantId: $plantId, from: $from, to: $to) {
      activeAlerts
      avgEfficiency
      totalActiveMachines
      alertDistribution {
        count
        severity
      }
      oeeTrend {
        availability
        performance
        quality
        time
      }
      productionTrend {
        actual
        target
        time
      }
      energyTrend {
        energy
        time
      }
      plants {
        plantId
        plantName
        machines
        efficiency
      }
    }
  }
`;

// Plant Dashboard query - specific to a plant with machines
export const PLANT_DASHBOARD_QUERY = gql`
  query PlantDashboard($plantId: UUID!, $from: DateTime, $to: DateTime) {
    plantDashboard(plantId: $plantId, from: $from, to: $to) {
      activeMachines
      avgRuntime
      plantEfficiency
      totalEnergy
      totalMachines
      energyTrend {
        energy
        time
      }
      productionTrend {
        actual
        target
        time
      }
      uptimeDowntime {
        label
        uptime
        downtime
      }
      machines {
        healthScore
        machineCode
        machineId
        machineName
        machineType
        runtimeHours
        status
        avgLoad
        currentLoad
      }
    }
  }
`;

// Machine Details query
export const MACHINE_DETAILS_QUERY = gql`
  query MachineDetails($plantId: UUID!, $machineId: UUID!, $from: DateTime, $to: DateTime) {
    machineDetails(plantId: $plantId, machineId: $machineId, from: $from, to: $to) {
      healthScore
      machineCode
      machineId
      machineName
      runtimeHours
      status
      alerts {
        parameter
        severity
        status
      }
      electrical {
        rVoltage
        yVoltage
        bVoltage
        rCurrent
        yCurrent
        bCurrent
        frequency
        powerFactor
        energyImportKwh
        energyImportKvah
        energyExportKwh
      }
      environmental {
        temperature
        humidity
        pressure
        flowRate
      }
      mechanical {
        rpm
        vibrationX
        vibrationY
        vibrationZ
      }
      systemHealth {
        overallHealth
        performanceIndex
        efficiencyScore
      }
      healthTrend {
        time
        value
      }
      loadTrend {
        time
        value
      }
      powerConsumptionTrend {
        time
        value
      }
      temperatureTrend {
        time
        value
      }
      vibrationTrend {
        time
        vibrationX
        vibrationY
        vibrationZ
      }
    }
  }
`;