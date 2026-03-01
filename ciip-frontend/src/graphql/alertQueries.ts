import { gql } from '@apollo/client';

export const ALERT_DASHBOARD_QUERY = gql`
  query AlertDashboard($plantId: UUID, $severity: String, $status: String, $fromDate: DateTime, $toDate: DateTime) {
    alertDashboard(plantId: $plantId, severity: $severity, status: $status, fromDate: $fromDate, toDate: $toDate) {
      acknowledged
      critical
      warning
      alerts {
        actualValue
        alertId
        generatedAt
        machineCode
        machineName
        parameter
        plantName
        severity
        status
      }
    }
  }
`;

export const ACKNOWLEDGED_ALERT_QUERY = gql`
  query AcknowledgedAlert($alertId: UUID!) {
    acknowledgedAlert(alertId: $alertId) {
      acknowledgedAt
      actionTaken
      alertId
      machineCode
      parameter
      plantName
      reason
      technicianName
    }
  }
`;

export const ACKNOWLEDGE_ALERT_MUTATION = gql`
  mutation AcknowledgeAlert(
    $actionTaken: String!
    $alertId: UUID!
    $reason: String!
    $technicianName: String!
  ) {
    acknowledgeAlert(
      input: {
        actionTaken: $actionTaken
        alertId: $alertId
        reason: $reason
        technicianName: $technicianName
      }
    )
  }
`;
