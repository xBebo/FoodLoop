// Workspace role for pages inside the shell, taken from the real session. Not authorization: the API decides.
import { createContext, useContext } from 'react'
import type { WorkspaceRole } from '../../app/routes'

export const WorkspaceRoleContext = createContext<WorkspaceRole>('beneficiary')
export const useWorkspaceRole = () => useContext(WorkspaceRoleContext)
