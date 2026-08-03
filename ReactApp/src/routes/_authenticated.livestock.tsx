import { createFileRoute } from '@tanstack/react-router'
import { Livestock } from '../pages/Livestock'

export const Route = createFileRoute('/_authenticated/livestock')({
  component: Livestock,
})
