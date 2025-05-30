﻿using FastEndpoints;
using LowPressureZone.Api.Extensions;
using LowPressureZone.Api.Rules;
using LowPressureZone.Domain;
using LowPressureZone.Identity.Extensions;
using Microsoft.EntityFrameworkCore;

namespace LowPressureZone.Api.Endpoints.Schedules.Timeslots;

public class PostTimeslot(DataContext dataContext, PerformerRules performerRules, ScheduleRules scheduleRules)
    : EndpointWithMapper<TimeslotRequest, TimeslotMapper>
{
    public override void Configure()
    {
        Post("/schedules/{scheduleId}/timeslots");
        Description(builder => builder.Produces(201));
    }

    public override async Task HandleAsync(TimeslotRequest request, CancellationToken ct)
    {
        var scheduleId = Route<Guid>("scheduleId");
        var schedule = await dataContext.Schedules
                                        .Include(schedule => schedule.Timeslots)
                                        .Include(schedule => schedule.Community)
                                        .ThenInclude(community => community.Relationships.Where(relationship => relationship.UserId == User.GetIdOrDefault()))
                                        .Where(schedule => schedule.Id == scheduleId)
                                        .FirstAsync(ct);

        var performer = await dataContext.Performers.FirstAsync(p => p.Id == request.PerformerId, ct);

        if (!scheduleRules.IsAddingTimeslotsAuthorized(schedule)
            || !performerRules.IsTimeslotLinkAuthorized(performer))
        {
            await SendUnauthorizedAsync(ct);
            return;
        }

        var timeslot = Map.ToEntity(request);
        dataContext.Timeslots.Add(timeslot);
        await dataContext.SaveChangesAsync(ct);
        HttpContext.ExposeLocation();
        await SendCreatedAtAsync<GetScheduleById>(new
        {
            id = scheduleId
        }, Response, cancellation: ct);
    }
}
