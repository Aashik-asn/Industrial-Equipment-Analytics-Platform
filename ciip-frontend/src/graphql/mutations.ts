const GRAPHQL_ENDPOINT = 'https://localhost:7035/graphql/';

async function executeMutation(query: string, variables: Record<string, unknown>) {
  const token = localStorage.getItem('token');
  
  try {
    const response = await fetch(GRAPHQL_ENDPOINT, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(token ? { Authorization: `Bearer ${token}` } : {}),
      },
      body: JSON.stringify({ query, variables }),
    });

    const result = await response.json();
    console.log('GraphQL response:', result);

    if (!response.ok) {
      const errorMsg = result.errors?.[0]?.message || `HTTP error! status: ${response.status}`;
      throw new Error(errorMsg);
    }

    if (result.errors) {
      throw new Error(result.errors[0].message);
    }
    
    return result.data;
  } catch (error) {
    if (error instanceof TypeError && error.message.includes('fetch')) {
      throw new Error('Failed to connect to server. Please check if the backend is running.');
    }
    throw error;
  }
}

export const LOGIN_MUTATION = `
  mutation Login($email: String!, $password: String!) {
    login(email: $email, password: $password) {
      token
      userId
      tenantId
      tenantName
      role
    }
  }
`;


export const REGISTER_TENANT_MUTATION = `
  mutation RegisterTenant($tenantName: String!, $email: String!, $password: String!) {
    registerTenant(tenantName: $tenantName, email: $email, password: $password) {
      tenantId
      tenantName
      email
      status
      createdAt
    }
  }
`;

// New Profile Page Mutations
export const CHANGE_PASSWORD_MUTATION = `
  mutation ChangePassword($newPassword: String!) {
    changePassword(newPassword: $newPassword)
  }
`;

export const UPDATE_PROFILE_MUTATION = `
  mutation UpdateProfile($email: String!) {
    updateProfile(email: $email) {
      createdAt
      email
      passwordHash
      role
      tenantId
      userId
    }
  }
`;

export const UPDATE_TENANT_NAME_MUTATION = `
  mutation UpdateTenantName($tenantName: String!) {
    updateTenantName(tenantName: $tenantName) {
      createdAt
      email
      passwordHash
      status
      tenantId
      tenantName
    }
  }
`;

export const UPSERT_PLANT_MUTATION = `
  mutation UpsertPlant($plantCode: String!, $plantName: String!, $city: String!) {
    upsertPlant(plantCode: $plantCode, plantName: $plantName, city: $city) {
      city
      createdAt
      plantCode
      plantId
      plantName
      status
      tenantId
    }
  }
`;

export const ADD_MACHINE_MUTATION = `
  mutation AddMachine($plantId: UUID!, $machineCode: String!, $machineName: String!, $machineType: String!, $gatewayCode: String!, $endpointType: String!, $protocol: String!) {
    addMachine(
      plantId: $plantId
      machineCode: $machineCode
      machineName: $machineName
      machineType: $machineType
      gatewayCode: $gatewayCode
      endpointType: $endpointType
      protocol: $protocol
    ) {
      createdAt
      machineCode
      machineId
      machineName
      machineType
      plantId
      status
    }
  }
`;

export const INSERT_THRESHOLD_MUTATION = `
  mutation InsertThreshold($input: ThresholdInput!) {
    insertThreshold(input: $input) {
      criticalValue
      machineType
      parameter
      tenantId
      warningValue
    }
  }
`;

export async function login(email: string, password: string) {
  return executeMutation(LOGIN_MUTATION, { email, password });
}

export async function registerTenant(tenantName: string, email: string, password: string) {
  return executeMutation(REGISTER_TENANT_MUTATION, { tenantName, email, password });
}
