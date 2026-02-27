import { gql } from '@apollo/client';

// Simple plants query for filter dropdown, sidebar, and quick access
// Backend Plant entity only has: plantId, plantName, plantCode, city, status, createdAt
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
